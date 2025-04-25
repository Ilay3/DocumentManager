// DocumentManager.Infrastructure/Services/TemplateManagerService.cs
using DocumentManager.Core.Entities;
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentManager.Infrastructure.Services
{
    /// <summary>
    /// Сервис для управления шаблонами документов с возможностью обновления из файлов без пересоздания БД
    /// </summary>
    public class TemplateManagerService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITemplateService _templateService;
        private readonly IJsonSchemaService _jsonSchemaService;
        private readonly ILogger<TemplateManagerService> _logger;
        private readonly string _jsonBasePath;
        private readonly string _templatesBasePath;

        public TemplateManagerService(
            ApplicationDbContext context,
            ITemplateService templateService,
            IJsonSchemaService jsonSchemaService,
            ILogger<TemplateManagerService> logger,
            string jsonBasePath,
            string templatesBasePath)
        {
            _context = context;
            _templateService = templateService;
            _jsonSchemaService = jsonSchemaService;
            _logger = logger;
            _jsonBasePath = jsonBasePath;
            _templatesBasePath = templatesBasePath;
        }

        /// <summary>
        /// Синхронизирует шаблоны из файловой системы с базой данных
        /// </summary>
        public async Task<SyncResult> SynchronizeTemplatesAsync()
        {
            var result = new SyncResult();

            try
            {
                _logger.LogInformation("Начинаем синхронизацию шаблонов документов...");
                _logger.LogInformation($"Путь к JSON-файлам: {_jsonBasePath}");
                _logger.LogInformation($"Путь к шаблонам Word: {_templatesBasePath}");

                // Проверяем существование директорий
                if (!Directory.Exists(_jsonBasePath))
                {
                    _logger.LogError($"Директория JSON-файлов не существует: {_jsonBasePath}");
                    result.Errors.Add($"Директория JSON-файлов не существует: {_jsonBasePath}");
                    return result;
                }

                if (!Directory.Exists(_templatesBasePath))
                {
                    _logger.LogError($"Директория шаблонов Word не существует: {_templatesBasePath}");
                    result.Errors.Add($"Директория шаблонов Word не существует: {_templatesBasePath}");
                    return result;
                }

                // Получаем все шаблоны в БД
                var dbTemplates = await _context.DocumentTemplates
                    .Include(t => t.Fields)
                    .ToListAsync();

                // Получаем все JSON-файлы из директории (включая поддиректории)
                var jsonFiles = Directory.GetFiles(_jsonBasePath, "*.json", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".bak"))
                    .ToList();

                _logger.LogInformation($"Найдено {jsonFiles.Count} JSON-файлов");

                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        var relativePath = Path.GetRelativePath(_jsonBasePath, jsonFile);
                        _logger.LogInformation($"Обработка файла: {relativePath}");

                        var fileContent = await File.ReadAllTextAsync(jsonFile);
                        JsonDocument jsonDoc = JsonDocument.Parse(fileContent);
                        JsonElement root = jsonDoc.RootElement;

                        // Проверяем наличие необходимых полей
                        if (root.TryGetProperty("id", out JsonElement idElement) &&
                            root.TryGetProperty("name", out JsonElement nameElement) &&
                            root.TryGetProperty("templatePath", out JsonElement templatePathElement))
                        {
                            var id = idElement.GetString();
                            var name = nameElement.GetString();
                            var templatePath = templatePathElement.GetString();

                            _logger.LogInformation($"ID: {id}, Name: {name}, TemplatePath: {templatePath}");

                            // Определяем тип документа
                            string documentType;
                            if (relativePath.Contains("Passport", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("ПС", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("Паспорт", StringComparison.OrdinalIgnoreCase))
                            {
                                documentType = "Passport";
                            }
                            else if (relativePath.Contains("PackingList", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("Уп. л.", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("Упаковочный", StringComparison.OrdinalIgnoreCase))
                            {
                                documentType = "PackingList";
                            }
                            else if (relativePath.Contains("PackingInventory", StringComparison.OrdinalIgnoreCase) ||
                                           name.Contains("Уп. вед.", StringComparison.OrdinalIgnoreCase) ||
                                           name.Contains("Упаковочная ведомость", StringComparison.OrdinalIgnoreCase))
                            {
                                documentType = "PackingInventory";
                            }
                            else
                            {
                                documentType = "Other";
                            }

                            // Проверяем существование шаблона Word
                            string wordTemplatePath = templatePath;
                            string fullWordTemplatePath = Path.Combine(_templatesBasePath, wordTemplatePath);

                            if (!File.Exists(fullWordTemplatePath))
                            {
                                // Ищем файл по имени в директории шаблонов
                                string fileName2 = Path.GetFileNameWithoutExtension(templatePath) + ".doc";
                                string fileName3 = Path.GetFileNameWithoutExtension(templatePath) + ".docx";

                                var wordFiles = Directory.GetFiles(_templatesBasePath, "*.doc*", SearchOption.AllDirectories);
                                var matchingFile = wordFiles.FirstOrDefault(f =>
                                    Path.GetFileName(f).Equals(fileName2, StringComparison.OrdinalIgnoreCase) ||
                                    Path.GetFileName(f).Equals(fileName3, StringComparison.OrdinalIgnoreCase) ||
                                    Path.GetFileName(f).StartsWith(id, StringComparison.OrdinalIgnoreCase));

                                if (matchingFile != null)
                                {
                                    wordTemplatePath = Path.GetRelativePath(_templatesBasePath, matchingFile);
                                    _logger.LogInformation($"Найден соответствующий шаблон Word: {wordTemplatePath}");
                                }
                                else
                                {
                                    _logger.LogWarning($"Шаблон Word не найден: {fullWordTemplatePath}");
                                    result.Warnings.Add($"Шаблон Word не найден: {fullWordTemplatePath}");
                                    continue;
                                }
                            }

                            // Ищем существующий шаблон в БД
                            var existingTemplate = dbTemplates.FirstOrDefault(t => t.Code == id);

                            if (existingTemplate != null)
                            {
                                // Обновляем существующий шаблон
                                existingTemplate.Name = name;
                                existingTemplate.Type = documentType;
                                existingTemplate.WordTemplatePath = wordTemplatePath;
                                existingTemplate.JsonSchemaPath = relativePath;
                                existingTemplate.IsActive = true;

                                _logger.LogInformation($"Обновление существующего шаблона: {name}");
                                result.Updated.Add(existingTemplate);

                                // Обновляем поля шаблона
                                await UpdateTemplateFieldsAsync(root, existingTemplate, result);
                            }
                            else
                            {
                                // Создаем новый шаблон
                                var newTemplate = new DocumentTemplate
                                {
                                    Code = id,
                                    Name = name,
                                    Type = documentType,
                                    WordTemplatePath = wordTemplatePath,
                                    JsonSchemaPath = relativePath,
                                    IsActive = true
                                };

                                _context.DocumentTemplates.Add(newTemplate);
                                await _context.SaveChangesAsync();

                                _logger.LogInformation($"Добавлен новый шаблон: {name}");
                                result.Added.Add(newTemplate);

                                // Добавляем поля для нового шаблона
                                await AddTemplateFieldsAsync(root, newTemplate, result);
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Некорректный формат JSON-файла: {jsonFile}");
                            result.Errors.Add($"Некорректный формат JSON-файла: {jsonFile}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Ошибка при обработке файла: {jsonFile}");
                        result.Errors.Add($"Ошибка при обработке файла {jsonFile}: {ex.Message}");
                    }
                }

                // Помечаем как неактивные шаблоны, которых нет в JSON-файлах
                var jsonIds = jsonFiles
                    .Select(f => {
                        try
                        {
                            var content = File.ReadAllText(f);
                            var doc = JsonDocument.Parse(content);
                            return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList();

                var missingTemplates = dbTemplates.Where(t => t.IsActive && !jsonIds.Contains(t.Code)).ToList();
                foreach (var template in missingTemplates)
                {
                    template.IsActive = false;
                    _logger.LogInformation($"Отключение отсутствующего шаблона: {template.Name}");
                    result.Deactivated.Add(template);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Синхронизация шаблонов завершена");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при синхронизации шаблонов");
                result.Errors.Add($"Ошибка при синхронизации шаблонов: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Добавляет поля для нового шаблона
        /// </summary>
        private async Task AddTemplateFieldsAsync(JsonElement root, DocumentTemplate template, SyncResult result)
        {
            if (root.TryGetProperty("fields", out JsonElement fieldsElement) && fieldsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var fieldElement in fieldsElement.EnumerateArray())
                {
                    try
                    {
                        var field = new DocumentField
                        {
                            DocumentTemplateId = template.Id,
                            FieldName = fieldElement.GetProperty("fieldName").GetString(),
                            FieldLabel = fieldElement.GetProperty("fieldLabel").GetString(),
                            FieldType = fieldElement.GetProperty("fieldType").GetString(),
                            IsRequired = fieldElement.TryGetProperty("isRequired", out var isRequiredElement) && isRequiredElement.GetBoolean(),
                            IsUnique = fieldElement.TryGetProperty("isUnique", out var isUniqueElement) && isUniqueElement.GetBoolean(),
                            DefaultValue = fieldElement.TryGetProperty("defaultValue", out var defaultValueElement) ? defaultValueElement.GetString() : null
                        };

                        // Добавляем опции, если они есть
                        if (fieldElement.TryGetProperty("options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Array)
                        {
                            var options = new List<string>();
                            foreach (var option in optionsElement.EnumerateArray())
                            {
                                options.Add(option.GetString());
                            }
                            field.Options = JsonSerializer.Serialize(options);
                        }

                        // Добавляем условия, если они есть
                        if (fieldElement.TryGetProperty("condition", out var conditionElement))
                        {
                            field.Condition = JsonSerializer.Serialize(conditionElement);
                        }

                        _context.DocumentFields.Add(field);
                        result.FieldsAdded++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Ошибка при добавлении поля для шаблона {template.Name}");
                        result.Errors.Add($"Ошибка при добавлении поля для шаблона {template.Name}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Добавлены поля для шаблона {template.Name}");
            }
        }

        /// <summary>
        /// Обновляет поля существующего шаблона
        /// </summary>
        private async Task UpdateTemplateFieldsAsync(JsonElement root, DocumentTemplate template, SyncResult result)
        {
            if (!root.TryGetProperty("fields", out JsonElement fieldsElement) || fieldsElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            // Получаем существующие поля шаблона
            var existingFields = await _context.DocumentFields
                .Where(f => f.DocumentTemplateId == template.Id)
                .ToListAsync();

            // Создаем словарь для быстрого поиска полей
            var existingFieldsDict = existingFields.ToDictionary(f => f.FieldName, f => f);

            // Обрабатываем каждое поле из JSON
            var processedFields = new HashSet<string>();

            foreach (var fieldElement in fieldsElement.EnumerateArray())
            {
                try
                {
                    var fieldName = fieldElement.GetProperty("fieldName").GetString();
                    processedFields.Add(fieldName);

                    if (existingFieldsDict.TryGetValue(fieldName, out var existingField))
                    {
                        // Обновляем существующее поле
                        existingField.FieldLabel = fieldElement.GetProperty("fieldLabel").GetString();
                        existingField.FieldType = fieldElement.GetProperty("fieldType").GetString();
                        existingField.IsRequired = fieldElement.TryGetProperty("isRequired", out var isRequiredElement) && isRequiredElement.GetBoolean();
                        existingField.IsUnique = fieldElement.TryGetProperty("isUnique", out var isUniqueElement) && isUniqueElement.GetBoolean();
                        existingField.DefaultValue = fieldElement.TryGetProperty("defaultValue", out var defaultValueElement) ? defaultValueElement.GetString() : null;

                        // Обновляем опции
                        if (fieldElement.TryGetProperty("options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Array)
                        {
                            var options = new List<string>();
                            foreach (var option in optionsElement.EnumerateArray())
                            {
                                options.Add(option.GetString());
                            }
                            existingField.Options = JsonSerializer.Serialize(options);
                        }
                        else
                        {
                            existingField.Options = null;
                        }

                        // Обновляем условия
                        if (fieldElement.TryGetProperty("condition", out var conditionElement))
                        {
                            existingField.Condition = JsonSerializer.Serialize(conditionElement);
                        }
                        else
                        {
                            existingField.Condition = null;
                        }

                        result.FieldsUpdated++;
                    }
                    else
                    {
                        // Добавляем новое поле
                        var newField = new DocumentField
                        {
                            DocumentTemplateId = template.Id,
                            FieldName = fieldName,
                            FieldLabel = fieldElement.GetProperty("fieldLabel").GetString(),
                            FieldType = fieldElement.GetProperty("fieldType").GetString(),
                            IsRequired = fieldElement.TryGetProperty("isRequired", out var isRequiredElement) && isRequiredElement.GetBoolean(),
                            IsUnique = fieldElement.TryGetProperty("isUnique", out var isUniqueElement) && isUniqueElement.GetBoolean(),
                            DefaultValue = fieldElement.TryGetProperty("defaultValue", out var defaultValueElement) ? defaultValueElement.GetString() : null
                        };

                        // Добавляем опции
                        if (fieldElement.TryGetProperty("options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Array)
                        {
                            var options = new List<string>();
                            foreach (var option in optionsElement.EnumerateArray())
                            {
                                options.Add(option.GetString());
                            }
                            newField.Options = JsonSerializer.Serialize(options);
                        }

                        // Добавляем условия
                        if (fieldElement.TryGetProperty("condition", out var conditionElement))
                        {
                            newField.Condition = JsonSerializer.Serialize(conditionElement);
                        }

                        _context.DocumentFields.Add(newField);
                        result.FieldsAdded++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка при обработке поля шаблона {template.Name}");
                    result.Errors.Add($"Ошибка при обработке поля шаблона {template.Name}: {ex.Message}");
                }
            }

            // Удаляем поля, которых нет в JSON
            var fieldsToRemove = existingFields.Where(f => !processedFields.Contains(f.FieldName)).ToList();
            foreach (var field in fieldsToRemove)
            {
                _context.DocumentFields.Remove(field);
                result.FieldsRemoved++;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Получает список файлов шаблонов Word
        /// </summary>
        public List<TemplateFileInfo> GetWordTemplateFiles()
        {
            var result = new List<TemplateFileInfo>();

            try
            {
                if (!Directory.Exists(_templatesBasePath))
                {
                    return result;
                }

                var files = Directory.GetFiles(_templatesBasePath, "*.doc*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(_templatesBasePath, file);
                    var info = new FileInfo(file);

                    result.Add(new TemplateFileInfo
                    {
                        Path = relativePath,
                        Name = Path.GetFileName(file),
                        LastModified = info.LastWriteTime,
                        Size = info.Length
                    });
                }

                return result.OrderBy(f => f.Path).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка файлов шаблонов Word");
                return result;
            }
        }

        /// <summary>
        /// Получает список файлов JSON-схем
        /// </summary>
        public List<TemplateFileInfo> GetJsonSchemaFiles()
        {
            var result = new List<TemplateFileInfo>();

            try
            {
                if (!Directory.Exists(_jsonBasePath))
                {
                    return result;
                }

                var files = Directory.GetFiles(_jsonBasePath, "*.json", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".bak"))
                    .ToArray();

                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(_jsonBasePath, file);
                    var info = new FileInfo(file);

                    // Пытаемся прочитать идентификатор из JSON
                    string id = null;
                    string name = null;
                    try
                    {
                        var content = File.ReadAllText(file);
                        var doc = JsonDocument.Parse(content);
                        id = doc.RootElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                        name = doc.RootElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    }
                    catch { /* Игнорируем ошибки чтения JSON */ }

                    result.Add(new TemplateFileInfo
                    {
                        Path = relativePath,
                        Name = Path.GetFileName(file),
                        LastModified = info.LastWriteTime,
                        Size = info.Length,
                        Id = id,
                        Description = name
                    });
                }

                return result.OrderBy(f => f.Path).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка файлов JSON-схем");
                return result;
            }
        }
    }

    /// <summary>
    /// Результат синхронизации шаблонов
    /// </summary>
    public class SyncResult
    {
        public List<DocumentTemplate> Added { get; set; } = new List<DocumentTemplate>();
        public List<DocumentTemplate> Updated { get; set; } = new List<DocumentTemplate>();
        public List<DocumentTemplate> Deactivated { get; set; } = new List<DocumentTemplate>();
        public int FieldsAdded { get; set; }
        public int FieldsUpdated { get; set; }
        public int FieldsRemoved { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        public bool HasErrors => Errors.Any();
        public bool HasWarnings => Warnings.Any();
        public bool HasChanges => Added.Any() || Updated.Any() || Deactivated.Any() || FieldsAdded > 0 || FieldsUpdated > 0 || FieldsRemoved > 0;

        public override string ToString()
        {
            return $"Добавлено шаблонов: {Added.Count}, Обновлено: {Updated.Count}, Деактивировано: {Deactivated.Count}, " +
                   $"Полей добавлено: {FieldsAdded}, Полей обновлено: {FieldsUpdated}, Полей удалено: {FieldsRemoved}, " +
                   $"Ошибок: {Errors.Count}, Предупреждений: {Warnings.Count}";
        }
    }

    /// <summary>
    /// Информация о файле шаблона
    /// </summary>
    public class TemplateFileInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
        public string Id { get; set; }
        public string Description { get; set; }
    }
}