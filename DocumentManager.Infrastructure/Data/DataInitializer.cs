using DocumentManager.Core.Entities;
using DocumentManager.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentManager.Infrastructure.Data
{
    public class DataInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly ITemplateService _templateService;
        private readonly IJsonSchemaService _jsonSchemaService;
        private readonly ILogger<DataInitializer> _logger;
        private readonly string _jsonBasePath;
        private readonly string _templatesBasePath;

        public DataInitializer(
            ApplicationDbContext context,
            ITemplateService templateService,
            IJsonSchemaService jsonSchemaService,
            ILogger<DataInitializer> logger,
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

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Начинаем инициализацию данных...");
                _logger.LogInformation($"Путь к JSON-файлам: {_jsonBasePath}");
                _logger.LogInformation($"Путь к шаблонам Word: {_templatesBasePath}");

                // Проверяем существование директорий
                if (!Directory.Exists(_jsonBasePath))
                {
                    _logger.LogError($"Директория JSON-файлов не существует: {_jsonBasePath}");
                    return;
                }

                if (!Directory.Exists(_templatesBasePath))
                {
                    _logger.LogError($"Директория шаблонов Word не существует: {_templatesBasePath}");
                    return;
                }

                // Проверяем, есть ли уже шаблоны в базе данных
                var templatesCount = await _context.DocumentTemplates.CountAsync();
                _logger.LogInformation($"Количество шаблонов в базе данных: {templatesCount}");

                if (templatesCount == 0)
                {
                    // Получаем все JSON-файлы из директории (включая поддиректории)
                    var jsonFiles = Directory.GetFiles(_jsonBasePath, "*.json", SearchOption.AllDirectories);
                    _logger.LogInformation($"Найдено {jsonFiles.Length} JSON-файлов");


                    foreach (var jsonFile in jsonFiles)
                    {
                        try
                        {
                            _logger.LogInformation($"Обработка файла: {jsonFile}");
                            var fileName = Path.GetFileName(jsonFile);
                            var relativePath = Path.GetRelativePath(_jsonBasePath, jsonFile);

                            var fileContent = await File.ReadAllTextAsync(jsonFile);
                            _logger.LogDebug($"Содержимое файла: {fileContent.Substring(0, Math.Min(fileContent.Length, 200))}...");

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
                                    }
                                }

                                // Создаем шаблон документа
                                var template = new DocumentTemplate
                                {
                                    Code = id,
                                    Name = name,
                                    Type = documentType,
                                    WordTemplatePath = wordTemplatePath,
                                    JsonSchemaPath = relativePath,
                                    IsActive = true
                                };

                                // Добавляем шаблон в базу данных
                                _context.DocumentTemplates.Add(template);
                                await _context.SaveChangesAsync();

                                _logger.LogInformation($"Шаблон добавлен: {template.Name}");

                                // Загружаем поля из JSON-схемы
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
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, $"Ошибка при добавлении поля для шаблона {template.Name}");
                                        }
                                    }

                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation($"Добавлены поля для шаблона {template.Name}");
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"Некорректный формат JSON-файла: {jsonFile}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Ошибка при обработке файла: {jsonFile}");
                        }
                    }

                    _logger.LogInformation("Инициализация данных завершена");
                }
                else
                {
                    _logger.LogInformation("Шаблоны уже загружены в базу данных");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации данных");
                throw;
            }
        }
    }
}