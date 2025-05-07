using DocumentManager.Core.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentManager.Infrastructure.Services
{
    /// <summary>
    /// Сервис для автоматической генерации JSON-схем на основе шаблонов Word
    /// </summary>
    public class TemplateJsonGeneratorService
    {
        private readonly ILogger<TemplateJsonGeneratorService> _logger;
        private readonly string _wordBasePath;
        private readonly string _jsonBasePath;

        public TemplateJsonGeneratorService(
            ILogger<TemplateJsonGeneratorService> logger,
            string wordBasePath,
            string jsonBasePath)
        {
            _logger = logger;
            _wordBasePath = wordBasePath;
            _jsonBasePath = jsonBasePath;
        }

        /// <summary>
        /// Генерирует JSON-схему на основе шаблона Word
        /// </summary>
        /// <param name="wordTemplatePath">Относительный путь к шаблону Word</param>
        /// <returns>Результат генерации JSON-схемы</returns>
        public async Task<JsonGenerationResult> GenerateJsonSchemaAsync(string wordTemplatePath)
        {
            var result = new JsonGenerationResult();

            try
            {
                // Полный путь к шаблону Word
                string fullWordPath = Path.Combine(_wordBasePath, wordTemplatePath);

                if (!File.Exists(fullWordPath))
                {
                    result.Success = false;
                    result.ErrorMessage = $"Файл шаблона не найден: {fullWordPath}";
                    return result;
                }

                // Получаем плейсхолдеры из шаблона Word
                List<string> placeholders = new List<string>();
                bool isDocx = Path.GetExtension(fullWordPath).ToLowerInvariant() == ".docx";

                if (isDocx)
                {
                    placeholders = await GetPlaceholdersFromDocxAsync(fullWordPath);
                }
                else
                {
                    // Используем существующий DocBinaryTemplateHandler для .doc файлов
                    var docHandler = new DocBinaryTemplateHandler(_logger);
                    placeholders = docHandler.FindPlaceholders(fullWordPath);
                }

                // Формируем соответствующий путь для JSON-файла
                string relativeDirectory = Path.GetDirectoryName(wordTemplatePath);
                string jsonDirectoryPath = Path.Combine(_jsonBasePath, relativeDirectory);
                string jsonFileName = Path.GetFileNameWithoutExtension(wordTemplatePath) + ".json";
                string jsonFilePath = Path.Combine(jsonDirectoryPath, jsonFileName);

                // Создаем директорию для JSON, если она не существует
                if (!Directory.Exists(jsonDirectoryPath))
                {
                    Directory.CreateDirectory(jsonDirectoryPath);
                    _logger.LogInformation($"Создана директория: {jsonDirectoryPath}");
                }

                // Формируем структуру JSON
                var jsonObject = CreateJsonSchema(placeholders, Path.GetFileName(wordTemplatePath), wordTemplatePath);

                // Сохраняем JSON в файл
                await SaveJsonSchemaAsync(jsonObject, jsonFilePath);

                // Заполняем результат
                result.Success = true;
                result.JsonFilePath = jsonFilePath;
                result.RelativeJsonPath = Path.Combine(relativeDirectory, jsonFileName);
                result.PlaceholdersCount = placeholders.Count;
                result.Placeholders = placeholders;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при генерации JSON-схемы для шаблона {wordTemplatePath}");
                result.Success = false;
                result.ErrorMessage = $"Ошибка при генерации JSON-схемы: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Получает плейсхолдеры из DOCX файла
        /// </summary>
        private async Task<List<string>> GetPlaceholdersFromDocxAsync(string docxPath)
        {
            // Используем Task.Run, так как OpenXML не имеет асинхронного API
            return await Task.Run(() =>
            {
                List<string> placeholders = new List<string>();
                HashSet<string> uniquePlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    using (WordprocessingDocument doc = WordprocessingDocument.Open(docxPath, false))
                    {
                        // Получаем весь текст документа
                        string docText = doc.MainDocumentPart.Document.Body.InnerText;

                        // Паттерны для различных форматов плейсхолдеров
                        var patterns = new[]
                        {
                            @"\{\{([^}]+)\}\}",  // Формат {{FieldName}}
                            @"<<([^>]+)>>",      // Формат <<FieldName>>
                            @"\[([^\]]+)\]",     // Формат [FieldName]
                            @"\$([a-zA-Z0-9_]+)" // Формат $FieldName
                        };

                        // Находим все плейсхолдеры по всем паттернам
                        foreach (var pattern in patterns)
                        {
                            var matches = Regex.Matches(docText, pattern);
                            foreach (Match match in matches)
                            {
                                if (match.Groups.Count > 1)
                                {
                                    string placeholder = match.Groups[1].Value.Trim();
                                    if (!string.IsNullOrWhiteSpace(placeholder) && !uniquePlaceholders.Contains(placeholder))
                                    {
                                        uniquePlaceholders.Add(placeholder);
                                        placeholders.Add(placeholder);
                                        _logger.LogDebug($"Найден плейсхолдер: {placeholder}");
                                    }
                                }
                            }
                        }

                        // Проверяем также содержимое всех элементов Run (текстовые фрагменты)
                        // для случаев, когда плейсхолдер разбит на несколько текстовых узлов
                        var paragraphs = doc.MainDocumentPart.Document.Descendants<Paragraph>();
                        foreach (var paragraph in paragraphs)
                        {
                            string paraText = paragraph.InnerText;
                            foreach (var pattern in patterns)
                            {
                                var matches = Regex.Matches(paraText, pattern);
                                foreach (Match match in matches)
                                {
                                    if (match.Groups.Count > 1)
                                    {
                                        string placeholder = match.Groups[1].Value.Trim();
                                        if (!string.IsNullOrWhiteSpace(placeholder) && !uniquePlaceholders.Contains(placeholder))
                                        {
                                            uniquePlaceholders.Add(placeholder);
                                            placeholders.Add(placeholder);
                                            _logger.LogDebug($"Найден плейсхолдер в параграфе: {placeholder}");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    _logger.LogInformation($"Всего найдено уникальных плейсхолдеров: {placeholders.Count}");
                    return placeholders;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка при извлечении плейсхолдеров из DOCX: {docxPath}");
                    throw;
                }
            });
        }

        /// <summary>
        /// Создает структуру JSON-схемы на основе плейсхолдеров
        /// </summary>
        private Dictionary<string, object> CreateJsonSchema(List<string> placeholders, string filename, string templatePath)
        {
            try
            {
                // Получаем код и имя шаблона из имени файла
                string code = Path.GetFileNameWithoutExtension(filename);
                string name = code;

                // Пытаемся извлечь тип документа из пути
                string documentType = "Other";
                string relPath = templatePath.Replace("\\", "/");

                if (relPath.Contains("Passport/", StringComparison.OrdinalIgnoreCase))
                    documentType = "Passport";
                else if (relPath.Contains("PackingList/", StringComparison.OrdinalIgnoreCase))
                    documentType = "PackingList";
                else if (relPath.Contains("PackingInventory/", StringComparison.OrdinalIgnoreCase))
                    documentType = "PackingInventory";

                // Создаем структуру JSON
                var jsonSchema = new Dictionary<string, object>
                {
                    ["id"] = code,
                    ["name"] = $"{code} ({documentType})",
                    ["templatePath"] = templatePath,
                    ["documentType"] = documentType,
                    ["created"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["fields"] = CreateFieldsArray(placeholders)
                };

                return jsonSchema;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании структуры JSON");
                throw;
            }
        }

        /// <summary>
        /// Создает массив полей для JSON-схемы
        /// </summary>
        private List<Dictionary<string, object>> CreateFieldsArray(List<string> placeholders)
        {
            var fields = new List<Dictionary<string, object>>();

            // Обязательное поле для заводского номера, если его нет среди плейсхолдеров
            bool hasFactoryNumber = placeholders.Any(p =>
                p.Equals("FactoryNumber", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("ЗаводскойНомер", StringComparison.OrdinalIgnoreCase));

            if (!hasFactoryNumber)
            {
                fields.Add(new Dictionary<string, object>
                {
                    ["fieldName"] = "FactoryNumber",
                    ["fieldLabel"] = "Заводской номер",
                    ["fieldType"] = "text",
                    ["isRequired"] = true,
                    ["isUnique"] = true,
                    ["defaultValue"] = ""
                });
            }

            // Добавляем остальные поля из плейсхолдеров
            foreach (var placeholder in placeholders)
            {
                // Определяем тип поля по содержимому плейсхолдера
                string fieldType = "text";
                bool isRequired = false;
                bool isUnique = false;
                string defaultValue = "";

                // Примитивное определение типа поля по имени
                if (placeholder.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
                    placeholder.Contains("Дата", StringComparison.OrdinalIgnoreCase))
                {
                    fieldType = "date";
                    defaultValue = DateTime.Now.ToString("yyyy-MM-dd");
                }
                else if (placeholder.Equals("FactoryNumber", StringComparison.OrdinalIgnoreCase) ||
                        placeholder.Contains("ЗаводскойНомер", StringComparison.OrdinalIgnoreCase))
                {
                    isRequired = true;
                    isUnique = true;
                }

                // Создаем удобочитаемую метку
                string fieldLabel = MakeHumanReadable(placeholder);

                // Добавляем поле в массив
                fields.Add(new Dictionary<string, object>
                {
                    ["fieldName"] = placeholder,
                    ["fieldLabel"] = fieldLabel,
                    ["fieldType"] = fieldType,
                    ["isRequired"] = isRequired,
                    ["isUnique"] = isUnique,
                    ["defaultValue"] = defaultValue
                });
            }

            return fields;
        }

        /// <summary>
        /// Преобразует идентификатор в человекочитаемый текст
        /// </summary>
        private string MakeHumanReadable(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return identifier;

            // Разбиваем CamelCase на слова
            string readable = Regex.Replace(identifier, "([A-Z])", " $1").Trim();

            // Обрабатываем snake_case и pascal_case
            readable = readable.Replace("_", " ");

            // Делаем первую букву заглавной
            if (readable.Length > 0)
            {
                readable = char.ToUpper(readable[0]) + readable.Substring(1);
            }

            return readable;
        }

        /// <summary>
        /// Сохраняет JSON-схему в файл
        /// </summary>
        private async Task SaveJsonSchemaAsync(Dictionary<string, object> jsonSchema, string filePath)
        {
            try
            {
                // Создаем бэкап, если файл уже существует
                if (File.Exists(filePath))
                {
                    string backupPath = filePath + ".bak";
                    File.Copy(filePath, backupPath, true);
                    _logger.LogInformation($"Создана резервная копия файла: {backupPath}");
                }

                // Сериализуем объект JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string jsonContent = JsonSerializer.Serialize(jsonSchema, options);

                // Сохраняем в файл
                await File.WriteAllTextAsync(filePath, jsonContent);
                _logger.LogInformation($"JSON-схема сохранена: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при сохранении JSON-схемы: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Результат генерации JSON-схемы
    /// </summary>
    public class JsonGenerationResult
    {
        /// <summary>
        /// Успешно ли завершилась генерация
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Полный путь к сгенерированному JSON-файлу
        /// </summary>
        public string JsonFilePath { get; set; }

        /// <summary>
        /// Относительный путь к JSON-файлу
        /// </summary>
        public string RelativeJsonPath { get; set; }

        /// <summary>
        /// Количество найденных плейсхолдеров
        /// </summary>
        public int PlaceholdersCount { get; set; }

        /// <summary>
        /// Список найденных плейсхолдеров
        /// </summary>
        public List<string> Placeholders { get; set; } = new List<string>();

        /// <summary>
        /// Сообщение об ошибке (если генерация не удалась)
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}