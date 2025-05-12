// DocumentManager.Infrastructure/Services/TemplateVersionMonitoringService.cs
using DocumentManager.Core.Entities;
using DocumentManager.Core.Interfaces;
using DocumentManager.Core.Models;
using DocumentManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocumentManager.Infrastructure.Services
{
    /// <summary>
    /// Сервис для мониторинга версий шаблонов документов
    /// </summary>
    public class TemplateVersionMonitoringService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITemplateService _templateService;
        private readonly ILogger<TemplateVersionMonitoringService> _logger;
        private readonly string _templatesBasePath;
        private readonly string _jsonBasePath;

        // Кэш версий файлов для отслеживания изменений
        private readonly ConcurrentDictionary<string, FileVersionInfo> _fileVersionCache;

        // Регулярное выражение для извлечения номера версии из скобок
        private readonly Regex _versionRegex = new Regex(@"\s*\((\d+)\)\s*$", RegexOptions.Compiled);

        public TemplateVersionMonitoringService(
            ApplicationDbContext context,
            ITemplateService templateService,
            ILogger<TemplateVersionMonitoringService> logger,
            string templatesBasePath,
            string jsonBasePath)
        {
            _context = context;
            _templateService = templateService;
            _logger = logger;
            _templatesBasePath = templatesBasePath;
            _jsonBasePath = jsonBasePath;
            _fileVersionCache = new ConcurrentDictionary<string, FileVersionInfo>();
        }

        /// <summary>
        /// Проверка шаблонов на изменения
        /// </summary>
        public async Task<List<TemplateVersionUpdate>> CheckForTemplateUpdatesAsync()
        {
            _logger.LogInformation("Начинаем проверку шаблонов на изменения версий");

            var updates = new List<TemplateVersionUpdate>();

            try
            {
                // Проверяем существование директории
                if (!Directory.Exists(_templatesBasePath))
                {
                    _logger.LogWarning($"Директория шаблонов не существует: {_templatesBasePath}");
                    return updates;
                }

                // Получаем все файлы шаблонов
                var templateFiles = Directory.GetFiles(_templatesBasePath, "*.doc*", SearchOption.AllDirectories);
                _logger.LogInformation($"Найдено {templateFiles.Length} файлов шаблонов");

                // Группируем файлы по базовому имени
                var fileGroups = new Dictionary<string, List<FileVersionInfo>>(StringComparer.OrdinalIgnoreCase);

                foreach (var file in templateFiles)
                {
                    try
                    {
                        var fileInfo = AnalyzeFile(file);

                        if (!fileGroups.ContainsKey(fileInfo.BaseFileName))
                        {
                            fileGroups[fileInfo.BaseFileName] = new List<FileVersionInfo>();
                        }

                        fileGroups[fileInfo.BaseFileName].Add(fileInfo);
                        _logger.LogDebug($"Проанализирован файл: {fileInfo.FileName}, базовое имя: {fileInfo.BaseFileName}, версия: {fileInfo.VersionNumber}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Ошибка при анализе файла: {file}");
                    }
                }

                // Проверяем каждую группу файлов на наличие новых версий
                foreach (var group in fileGroups)
                {
                    var baseFile = group.Key;
                    var versions = group.Value.OrderBy(v => v.VersionNumber ?? 0).ToList();

                    _logger.LogDebug($"Группа '{baseFile}': найдено {versions.Count} версий");

                    // Если есть только один файл без версии, пропускаем
                    if (versions.Count == 1 && !versions.First().VersionNumber.HasValue)
                    {
                        _logger.LogDebug($"Пропускаем единственный файл без версии: {baseFile}");
                        continue;
                    }

                    // Если есть файлы с версиями, или несколько файлов с одним базовым именем
                    if (versions.Any(v => v.VersionNumber.HasValue) || versions.Count > 1)
                    {
                        // Находим файл с максимальной версией
                        var latestVersion = versions.LastOrDefault();

                        if (latestVersion != null)
                        {
                            _logger.LogDebug($"Проверяем обновления для '{baseFile}', последняя версия: {latestVersion.VersionNumber}");

                            // Проверяем, есть ли соответствующий шаблон в базе данных
                            var update = await CheckTemplateForUpdateAsync(baseFile, latestVersion);

                            if (update != null)
                            {
                                updates.Add(update);
                                _logger.LogInformation($"Найдено обновление: {update.TemplateCode} ({update.OldVersion} -> {update.NewVersion})");
                            }
                        }
                    }
                }

                _logger.LogInformation($"Обнаружено {updates.Count} обновлений шаблонов");
                return updates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке шаблонов на изменения");
                throw;
            }
        }

        /// <summary>
        /// Анализ файла для извлечения информации о версии
        /// </summary>
        private FileVersionInfo AnalyzeFile(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
            var relativePath = Path.GetRelativePath(_templatesBasePath, filePath).Replace("\\", "/");

            // Извлекаем номер версии из скобок
            var versionMatch = _versionRegex.Match(fileName);
            int? versionNumber = null;
            string baseFileName = fileName;

            if (versionMatch.Success)
            {
                if (int.TryParse(versionMatch.Groups[1].Value, out var version))
                {
                    versionNumber = version;
                    // Убираем версию и лишние пробелы из базового имени
                    baseFileName = fileName.Substring(0, versionMatch.Index).Trim();
                }
            }

            return new FileVersionInfo
            {
                FilePath = filePath,
                RelativePath = relativePath,
                FileName = fileName,
                VersionNumber = versionNumber,
                LastModified = fileInfo.LastWriteTime,
                FileSize = fileInfo.Length,
                BaseFileName = baseFileName
            };
        }

        /// <summary>
        /// Проверка конкретного шаблона на необходимость обновления
        /// </summary>
        private async Task<TemplateVersionUpdate> CheckTemplateForUpdateAsync(string baseFileName, FileVersionInfo latestVersion)
        {
            try
            {
                _logger.LogDebug($"Ищем шаблон для базового имени: {baseFileName}");

                // Загружаем все активные шаблоны на клиентскую сторону для совместимости с PostgreSQL
                var allTemplates = await _context.DocumentTemplates
                    .Where(t => t.IsActive)
                    .ToListAsync();

                // Ищем подходящий шаблон
                var candidateTemplates = allTemplates.Where(t =>
                {
                    // Проверяем Code
                    if (t.Code.Equals(baseFileName, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Проверяем WordTemplatePath
                    var templateFileName = Path.GetFileNameWithoutExtension(t.WordTemplatePath);
                    if (!string.IsNullOrEmpty(templateFileName))
                    {
                        // Убираем версию из пути в базе, если есть
                        var versionMatch = _versionRegex.Match(templateFileName);
                        if (versionMatch.Success)
                        {
                            templateFileName = templateFileName.Substring(0, versionMatch.Index).Trim();
                        }

                        if (templateFileName.Equals(baseFileName, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }

                    return false;
                }).ToList();

                if (!candidateTemplates.Any())
                {
                    _logger.LogDebug($"Шаблон для файла {baseFileName} не найден в базе данных");
                    return null;
                }

                // Если нашли несколько кандидатов, выбираем наиболее подходящий
                var template = candidateTemplates.FirstOrDefault(t =>
                    t.Code.Equals(baseFileName, StringComparison.OrdinalIgnoreCase))
                    ?? candidateTemplates.First();

                _logger.LogDebug($"Найден шаблон: {template.Code} (ID: {template.Id})");

                // Проверяем, есть ли кэшированная информация о версии
                var cacheKey = $"{template.Id}_{baseFileName}";
                var cachedVersion = _fileVersionCache.GetValueOrDefault(cacheKey);

                // Анализируем текущий путь шаблона на версию
                var currentFilePath = Path.Combine(_templatesBasePath, template.WordTemplatePath);
                FileVersionInfo currentVersionInfo = null;

                if (File.Exists(currentFilePath))
                {
                    currentVersionInfo = AnalyzeFile(currentFilePath);
                }
                else
                {
                    _logger.LogWarning($"Файл шаблона не найден: {currentFilePath}");
                    // Создаем пустую информацию о версии
                    currentVersionInfo = new FileVersionInfo
                    {
                        FilePath = currentFilePath,
                        RelativePath = template.WordTemplatePath,
                        FileName = Path.GetFileNameWithoutExtension(template.WordTemplatePath),
                        VersionNumber = null,
                        LastModified = DateTime.MinValue,
                        FileSize = 0,
                        BaseFileName = baseFileName
                    };
                }

                // Проверяем, нужно ли обновление
                bool needsUpdate = false;
                string reason = "";

                // 1. Если новая версия больше текущей
                if (latestVersion.VersionNumber.HasValue)
                {
                    if (!currentVersionInfo.VersionNumber.HasValue)
                    {
                        needsUpdate = true;
                        reason = $"Появилась новая версия {latestVersion.VersionNumber} (ранее версии не было)";
                    }
                    else if (latestVersion.VersionNumber.Value > currentVersionInfo.VersionNumber.Value)
                    {
                        needsUpdate = true;
                        reason = $"Новая версия {latestVersion.VersionNumber} > текущая {currentVersionInfo.VersionNumber}";
                    }
                }

                // 2. Если файл изменился по времени или размеру (для файлов без версий)
                if (!needsUpdate && cachedVersion != null)
                {
                    if (latestVersion.LastModified != cachedVersion.LastModified)
                    {
                        needsUpdate = true;
                        reason = $"Файл изменился по времени: {latestVersion.LastModified} vs {cachedVersion.LastModified}";
                    }
                    else if (latestVersion.FileSize != cachedVersion.FileSize)
                    {
                        needsUpdate = true;
                        reason = $"Файл изменился по размеру: {latestVersion.FileSize} vs {cachedVersion.FileSize}";
                    }
                }

                // 3. Если текущий файл не существует
                if (!needsUpdate && !File.Exists(currentFilePath))
                {
                    needsUpdate = true;
                    reason = "Текущий файл шаблона не существует";
                }

                // Обновляем кэш
                _fileVersionCache[cacheKey] = latestVersion;

                if (needsUpdate)
                {
                    _logger.LogInformation($"Обнаружено обновление для шаблона {template.Code}: {reason}");

                    return new TemplateVersionUpdate
                    {
                        TemplateId = template.Id,
                        TemplateCode = template.Code,
                        OldPath = template.WordTemplatePath,
                        NewPath = latestVersion.RelativePath,
                        OldVersion = currentVersionInfo.VersionNumber,
                        NewVersion = latestVersion.VersionNumber,
                        DetectedAt = DateTime.UtcNow,
                        IsApplied = false
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при проверке шаблона {baseFileName}");
                return null;
            }
        }

        /// <summary>
        /// Применение обновлений шаблонов
        /// </summary>
        public async Task<bool> ApplyTemplateUpdatesAsync(List<TemplateVersionUpdate> updates)
        {
            _logger.LogInformation($"Применяем {updates.Count} обновлений шаблонов");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var update in updates)
                {
                    var template = await _context.DocumentTemplates.FindAsync(update.TemplateId);

                    if (template != null)
                    {
                        _logger.LogInformation($"Обновляем шаблон {template.Code}: {update.OldPath} -> {update.NewPath}");

                        // Обновляем путь к шаблону
                        template.WordTemplatePath = update.NewPath;

                        // Также обновляем JSON-схему, если она соответствует старому пути
                        await UpdateJsonSchemaPathAsync(template, update);

                        _context.DocumentTemplates.Update(template);
                        update.IsApplied = true;
                    }
                    else
                    {
                        _logger.LogWarning($"Шаблон с ID {update.TemplateId} не найден");
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Все обновления шаблонов применены успешно");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при применении обновлений шаблонов");
                await transaction.RollbackAsync();
                return false;
            }
        }

        /// <summary>
        /// Обновление пути в JSON-схеме
        /// </summary>
        private async Task UpdateJsonSchemaPathAsync(DocumentTemplate template, TemplateVersionUpdate update)
        {
            try
            {
                var jsonPath = Path.Combine(_jsonBasePath, template.JsonSchemaPath);

                if (File.Exists(jsonPath))
                {
                    _logger.LogInformation($"Обновляем JSON-схему: {jsonPath}");

                    var jsonContent = await File.ReadAllTextAsync(jsonPath);
                    var jsonDoc = JsonDocument.Parse(jsonContent);

                    // Создаем новый JSON объект с обновленным templatePath
                    var jsonObject = new Dictionary<string, object>();

                    foreach (var property in jsonDoc.RootElement.EnumerateObject())
                    {
                        if (property.Name == "templatePath")
                        {
                            jsonObject[property.Name] = update.NewPath;
                        }
                        else
                        {
                            // Десериализуем значение в правильный тип
                            jsonObject[property.Name] = property.Value.ValueKind switch
                            {
                                JsonValueKind.String => property.Value.GetString(),
                                JsonValueKind.Number when property.Value.TryGetInt32(out var intVal) => intVal,
                                JsonValueKind.Number => property.Value.GetDecimal(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Null => null,
                                JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(property.Value.GetRawText()),
                                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(property.Value.GetRawText()),
                                _ => property.Value.GetRawText()
                            };
                        }
                    }

                    // Сохраняем обновленный JSON
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    var updatedJsonContent = JsonSerializer.Serialize(jsonObject, options);
                    await File.WriteAllTextAsync(jsonPath, updatedJsonContent);

                    _logger.LogInformation($"JSON-схема обновлена успешно");
                }
                else
                {
                    _logger.LogWarning($"JSON-схема не найдена: {jsonPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обновлении JSON-схемы для шаблона {template.Code}");
            }
        }

        /// <summary>
        /// Получение истории версий для файла
        /// </summary>
        public List<FileVersionInfo> GetFileVersionHistory(string baseFileName)
        {
            try
            {
                if (!Directory.Exists(_templatesBasePath))
                {
                    return new List<FileVersionInfo>();
                }

                var templateFiles = Directory.GetFiles(_templatesBasePath, "*.doc*", SearchOption.AllDirectories);

                return templateFiles
                    .Select(AnalyzeFile)
                    .Where(f => f.BaseFileName.Equals(baseFileName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.VersionNumber ?? 0)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении истории версий для файла {baseFileName}");
                return new List<FileVersionInfo>();
            }
        }

        /// <summary>
        /// Принудительная очистка кэша
        /// </summary>
        public void ClearCache()
        {
            _fileVersionCache.Clear();
            _logger.LogInformation("Кэш версий файлов очищен");
        }

        /// <summary>
        /// Получение статистики кэша
        /// </summary>
        public Dictionary<string, object> GetCacheStatistics()
        {
            return new Dictionary<string, object>
            {
                { "CacheSize", _fileVersionCache.Count },
                { "CacheKeys", _fileVersionCache.Keys.ToList() }
            };
        }
    }
}