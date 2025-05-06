// DocumentManager.Web/Controllers/TemplatesAdminController.cs
using DocumentManager.Core.Entities;
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Services;
using DocumentManager.Web.Helpers;
using DocumentManager.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;


namespace DocumentManager.Web.Controllers
{
    public class TemplatesAdminController : Controller
    {
        private readonly ITemplateService _templateService;
        private readonly TemplateManagerService _templateManagerService;
        private readonly ILogger<TemplatesAdminController> _logger;
        private readonly string _jsonBasePath;
        private readonly string _templatesBasePath;
        private readonly IWebHostEnvironment _environment;
        private readonly IDocumentGenerationService _documentGenerationService;

        public TemplatesAdminController(
            ITemplateService templateService,
            TemplateManagerService templateManagerService,
            ILogger<TemplatesAdminController> logger,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            IDocumentGenerationService documentGenerationService)
        {
            _templateService = templateService;
            _templateManagerService = templateManagerService;
            _logger = logger;
            _environment = environment;
            _documentGenerationService = documentGenerationService;

            // Получаем пути к директориям из конфигурации
            _jsonBasePath = Path.Combine(_environment.WebRootPath, configuration["JsonBasePath"] ?? "Templates/Json");
            _templatesBasePath = Path.Combine(_environment.WebRootPath, configuration["TemplatesBasePath"] ?? "Templates/Word");
        }


        // GET: TemplatesAdmin
        public async Task<IActionResult> Index(string searchTerm = null, string type = null, bool? isActive = null, string sortBy = "Name", string sortDir = "asc")
        {
            try
            {
                var templates = await _templateService.GetAllTemplatesAsync(true);

                var viewModel = new TemplatesAdminViewModel
                {
                    Templates = templates.Select(t => new DocumentTemplateViewModel
                    {
                        Id = t.Id,
                        Code = t.Code,
                        Name = t.Name,
                        Type = t.Type,
                        IsActive = t.IsActive,
                        JsonSchemaPath = t.JsonSchemaPath,
                        WordTemplatePath = t.WordTemplatePath
                    }).ToList(),
                    SearchTerm = searchTerm,
                    TypeFilter = type,
                    IsActiveFilter = isActive,
                    SortBy = sortBy,
                    SortDirection = sortDir
                };

                // Применяем фильтры
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    viewModel.Templates = viewModel.Templates
                        .Where(t =>
                            t.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                            t.Code.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (!string.IsNullOrEmpty(type))
                {
                    viewModel.Templates = viewModel.Templates
                        .Where(t => t.Type == type)
                        .ToList();
                }

                if (isActive.HasValue)
                {
                    viewModel.Templates = viewModel.Templates
                        .Where(t => t.IsActive == isActive.Value)
                        .ToList();
                }

                // Применяем сортировку
                viewModel.Templates = SortTemplates(viewModel.Templates, sortBy, sortDir).ToList();

                // Заполняем список доступных типов для фильтра
                viewModel.AvailableTypes = templates.Select(t => t.Type).Distinct().ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка шаблонов");
                TempData["ErrorMessage"] = $"Ошибка при получении списка шаблонов: {ex.Message}";
                return View(new TemplatesAdminViewModel());
            }
        }

        // GET: TemplatesAdmin/RelatedTemplates/5
        public async Task<IActionResult> RelatedTemplates(int id)
        {
            try
            {
                var template = await _templateService.GetTemplateByIdAsync(id);

                if (template == null)
                {
                    TempData["ErrorMessage"] = $"Шаблон с ID {id} не найден";
                    return RedirectToAction(nameof(Index));
                }

                // Получаем все шаблоны, чтобы искать связанные
                var allTemplates = await _templateService.GetAllTemplatesAsync(true);

                // Используем вспомогательный класс для определения связанных шаблонов
                var relatedTemplates = DocumentRelationHelper.FindRelatedTemplates(template, allTemplates);

                var viewModel = new RelatedTemplatesViewModel
                {
                    MainTemplate = new DocumentTemplateViewModel
                    {
                        Id = template.Id,
                        Code = template.Code,
                        Name = template.Name,
                        Type = template.Type,
                        IsActive = template.IsActive
                    },
                    RelatedTemplates = relatedTemplates.Select(t => new DocumentTemplateViewModel
                    {
                        Id = t.Id,
                        Code = t.Code,
                        Name = t.Name,
                        Type = t.Type,
                        IsActive = t.IsActive
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении связанных шаблонов для ID {id}");
                TempData["ErrorMessage"] = $"Ошибка: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: TemplatesAdmin/ValidateTemplate/5
        public async Task<IActionResult> ValidateTemplate(int id)
        {
            try
            {
                var template = await _templateService.GetTemplateByIdAsync(id);

                if (template == null)
                {
                    TempData["ErrorMessage"] = $"Шаблон с ID {id} не найден";
                    return RedirectToAction(nameof(Index));
                }

                // Получаем поля шаблона
                var fields = await _templateService.GetTemplateFieldsAsync(id);

                // Получаем полный путь к шаблону Word
                string wordTemplatePath = Path.Combine(_templatesBasePath, template.WordTemplatePath);

                // Проверяем наличие файла шаблона
                bool templateExists = System.IO.File.Exists(wordTemplatePath);

                // Получаем плейсхолдеры из шаблона Word
                List<string> placeholders = new List<string>();

                if (templateExists)
                {
                    // Для файлов .doc используем DocBinaryTemplateHandler
                    if (Path.GetExtension(wordTemplatePath).ToLowerInvariant() == ".doc")
                    {
                        var docHandler = new DocBinaryTemplateHandler(_logger);
                        placeholders = docHandler.FindPlaceholders(wordTemplatePath);
                    }
                    // Для .docx можно использовать DocX
                    else if (Path.GetExtension(wordTemplatePath).ToLowerInvariant() == ".docx")
                    {
                        try
                        {
                            using (WordprocessingDocument doc = WordprocessingDocument.Open(wordTemplatePath, false))
                            {
                                string text = doc.MainDocumentPart.Document.Body.InnerText;
                                var matches = Regex.Matches(text, @"\{\{([^}]+)\}\}");
                                foreach (Match match in matches)
                                {
                                    if (match.Groups.Count > 1)
                                    {
                                        placeholders.Add(match.Groups[1].Value);
                                    }
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Ошибка при чтении шаблона DOCX: {wordTemplatePath}");
                        }
                    }
                }

                // Создаем список полей из шаблона
                var fieldNames = fields.Select(f => f.FieldName).ToList();

                // Находим плейсхолдеры, которых нет в полях
                var missingFields = placeholders.Except(fieldNames, StringComparer.OrdinalIgnoreCase).ToList();

                // Находим поля, которых нет в плейсхолдерах
                var unusedFields = fieldNames.Except(placeholders, StringComparer.OrdinalIgnoreCase).ToList();

                var viewModel = new TemplateValidationViewModel
                {
                    Template = new DocumentTemplateViewModel
                    {
                        Id = template.Id,
                        Code = template.Code,
                        Name = template.Name,
                        Type = template.Type,
                        IsActive = template.IsActive,
                        WordTemplatePath = template.WordTemplatePath,
                        JsonSchemaPath = template.JsonSchemaPath
                    },
                    TemplateExists = templateExists,
                    Fields = fields.Select(f => new DocumentFieldViewModel
                    {
                        Id = f.Id,
                        FieldName = f.FieldName,
                        FieldLabel = f.FieldLabel,
                        FieldType = f.FieldType,
                        IsRequired = f.IsRequired,
                        IsUnique = f.IsUnique
                    }).ToList(),
                    Placeholders = placeholders,
                    MissingFields = missingFields,
                    UnusedFields = unusedFields,
                    IsValid = !missingFields.Any() && templateExists
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при валидации шаблона ID {id}");
                TempData["ErrorMessage"] = $"Ошибка: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: TemplatesAdmin/PreviewTemplate/5
        public async Task<IActionResult> PreviewTemplate(int id)
        {
            try
            {
                var template = await _templateService.GetTemplateByIdAsync(id);

                if (template == null)
                {
                    TempData["ErrorMessage"] = $"Шаблон с ID {id} не найден";
                    return RedirectToAction(nameof(Index));
                }

                // Получаем поля шаблона
                var fields = await _templateService.GetTemplateFieldsAsync(id);

                // Создаем модель для предпросмотра с тестовыми значениями
                var viewModel = new TemplatePreviewViewModel
                {
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    TemplateCode = template.Code,
                    TemplateType = template.Type,
                    WordTemplatePath = template.WordTemplatePath,
                    Fields = fields.Select(f => new DocumentFieldViewModel
                    {
                        Id = f.Id,
                        FieldName = f.FieldName,
                        FieldLabel = f.FieldLabel,
                        FieldType = f.FieldType,
                        IsRequired = f.IsRequired,
                        IsUnique = f.IsUnique,
                        DefaultValue = f.DefaultValue,
                        Value = GetTestValueForField(f) // Генерируем тестовое значение
                    }).ToList()
                };

                // Заполняем тестовые значения для предпросмотра
                var testValues = new Dictionary<string, string>();
                foreach (var field in viewModel.Fields)
                {
                    testValues[field.FieldName] = field.Value;
                }

                viewModel.TestValues = testValues;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при предпросмотре шаблона ID {id}");
                TempData["ErrorMessage"] = $"Ошибка: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // Вспомогательный метод для генерации тестовых значений
        private string GetTestValueForField(DocumentField field)
        {
            // Если есть значение по умолчанию - используем его
            if (!string.IsNullOrEmpty(field.DefaultValue))
            {
                return field.DefaultValue;
            }

            // Иначе генерируем тестовое значение в зависимости от типа поля
            switch (field.FieldType.ToLower())
            {
                case "date":
                    return DateTime.Now.ToString("yyyy-MM-dd");

                case "select":
                    // Получаем первое значение из списка опций (если есть)
                    if (!string.IsNullOrEmpty(field.Options))
                    {
                        try
                        {
                            var options = System.Text.Json.JsonSerializer.Deserialize<List<string>>(field.Options);
                            if (options != null && options.Any())
                            {
                                return options.First();
                            }
                        }
                        catch { }
                    }
                    return "Тестовое значение";

                default:
                    // Для FactoryNumber сделаем специальное значение
                    if (field.FieldName.Contains("FactoryNumber", StringComparison.OrdinalIgnoreCase))
                    {
                        return "ТЕСТ-" + DateTime.Now.ToString("yyyyMMdd");
                    }

                    // Для других полей используем имя поля
                    return "Тестовое значение для " + field.FieldLabel;
            }
        }

        // POST: TemplatesAdmin/GenerateTestDocument
        [HttpPost]
        public async Task<IActionResult> GenerateTestDocument(int templateId, Dictionary<string, string> fieldValues)
        {
            try
            {
                var template = await _templateService.GetTemplateByIdAsync(templateId);

                if (template == null)
                {
                    TempData["ErrorMessage"] = $"Шаблон с ID {templateId} не найден";
                    return RedirectToAction(nameof(Index));
                }

                // Получаем путь к шаблону Word
                string templatePath = template.WordTemplatePath;

                // Генерируем имя выходного файла
                string outputFileName = $"ТЕСТ_{template.Code}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(templatePath)}";

                // Создаем тестовый документ
                var result = await _documentGenerationService.GenerateDocumentAsync(templatePath, fieldValues, outputFileName);

                // Получаем путь к созданному файлу и его содержимое
                string filePath = result.FilePath;
                byte[] content = result.Content;

                // Возвращаем файл пользователю для скачивания
                return File(content, GetContentType(filePath), Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при генерации тестового документа для шаблона ID {templateId}");
                TempData["ErrorMessage"] = $"Ошибка: {ex.Message}";
                return RedirectToAction(nameof(PreviewTemplate), new { id = templateId });
            }
        }

        private string GetContentType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream",
            };
        }

        private IEnumerable<DocumentTemplateViewModel> SortTemplates(IEnumerable<DocumentTemplateViewModel> templates, string sortBy, string sortDir)
        {
            IOrderedEnumerable<DocumentTemplateViewModel> sortedTemplates;

            switch (sortBy.ToLower())
            {
                case "code":
                    sortedTemplates = sortDir.ToLower() == "asc" ?
                        templates.OrderBy(t => t.Code) :
                        templates.OrderByDescending(t => t.Code);
                    break;
                case "type":
                    sortedTemplates = sortDir.ToLower() == "asc" ?
                        templates.OrderBy(t => t.Type) :
                        templates.OrderByDescending(t => t.Type);
                    break;
                case "status":
                    sortedTemplates = sortDir.ToLower() == "asc" ?
                        templates.OrderBy(t => t.IsActive) :
                        templates.OrderByDescending(t => t.IsActive);
                    break;
                case "name":
                default:
                    sortedTemplates = sortDir.ToLower() == "asc" ?
                        templates.OrderBy(t => t.Name) :
                        templates.OrderByDescending(t => t.Name);
                    break;
            }

            return sortedTemplates;
        }

        // GET: TemplatesAdmin/Sync
        public async Task<IActionResult> Sync()
        {
            try
            {
                var result = await _templateManagerService.SynchronizeTemplatesAsync();

                if (result.HasErrors)
                {
                    foreach (var error in result.Errors)
                    {
                        TempData["ErrorMessage"] = error;
                    }
                }

                if (result.HasWarnings)
                {
                    foreach (var warning in result.Warnings)
                    {
                        TempData["WarningMessage"] = warning;
                    }
                }

                if (result.HasChanges)
                {
                    TempData["SuccessMessage"] = $"Синхронизация завершена: добавлено {result.Added.Count} шаблонов, " +
                        $"обновлено {result.Updated.Count}, деактивировано {result.Deactivated.Count}, " +
                        $"добавлено {result.FieldsAdded} полей, обновлено {result.FieldsUpdated} полей, удалено {result.FieldsRemoved} полей.";
                }
                else
                {
                    TempData["InfoMessage"] = "Синхронизация завершена без изменений.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при синхронизации шаблонов");
                TempData["ErrorMessage"] = $"Ошибка при синхронизации шаблонов: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: TemplatesAdmin/Files
        public IActionResult Files()
        {
            try
            {
                var viewModel = new TemplateFilesViewModel
                {
                    WordTemplates = _templateManagerService.GetWordTemplateFiles(),
                    JsonSchemas = _templateManagerService.GetJsonSchemaFiles()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка файлов шаблонов");
                TempData["ErrorMessage"] = $"Ошибка при получении списка файлов шаблонов: {ex.Message}";
                return View(new TemplateFilesViewModel());
            }
        }

        // POST: TemplatesAdmin/UploadWordTemplate
        [HttpPost]
        public async Task<IActionResult> UploadWordTemplate(IFormFile file, string subFolder = "")
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Файл не выбран";
                return RedirectToAction(nameof(Files));
            }

            try
            {
                // Проверяем расширение файла
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".doc" && extension != ".docx")
                {
                    TempData["ErrorMessage"] = "Разрешены только файлы .doc и .docx";
                    return RedirectToAction(nameof(Files));
                }

                // Создаем директорию, если нужно
                var targetFolder = _templatesBasePath;
                if (!string.IsNullOrEmpty(subFolder))
                {
                    targetFolder = Path.Combine(targetFolder, subFolder);
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }
                }

                // Определяем путь для сохранения файла
                var filePath = Path.Combine(targetFolder, file.FileName);

                // Сохраняем файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                TempData["SuccessMessage"] = $"Файл {file.FileName} успешно загружен";
                return RedirectToAction(nameof(Files));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрузке файла {file.FileName}");
                TempData["ErrorMessage"] = $"Ошибка при загрузке файла: {ex.Message}";
                return RedirectToAction(nameof(Files));
            }
        }

        // POST: TemplatesAdmin/UploadJsonSchema
        [HttpPost]
        public async Task<IActionResult> UploadJsonSchema(IFormFile file, string subFolder = "")
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Файл не выбран";
                return RedirectToAction(nameof(Files));
            }

            try
            {
                // Проверяем расширение файла
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".json")
                {
                    TempData["ErrorMessage"] = "Разрешены только файлы .json";
                    return RedirectToAction(nameof(Files));
                }

                // Создаем директорию, если нужно
                var targetFolder = _jsonBasePath;
                if (!string.IsNullOrEmpty(subFolder))
                {
                    targetFolder = Path.Combine(targetFolder, subFolder);
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }
                }

                // Определяем путь для сохранения файла
                var filePath = Path.Combine(targetFolder, file.FileName);

                // Создаем резервную копию, если файл существует
                if (System.IO.File.Exists(filePath))
                {
                    var backupPath = filePath + ".bak";
                    System.IO.File.Copy(filePath, backupPath, true);
                    _logger.LogInformation($"Создана резервная копия файла: {backupPath}");
                }

                // Сохраняем файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                TempData["SuccessMessage"] = $"Файл {file.FileName} успешно загружен";
                return RedirectToAction(nameof(Files));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрузке файла {file.FileName}");
                TempData["ErrorMessage"] = $"Ошибка при загрузке файла: {ex.Message}";
                return RedirectToAction(nameof(Files));
            }
        }

        // GET: TemplatesAdmin/ViewFile
        public IActionResult ViewFile(string type, string path)
        {
            try
            {
                string fullPath;
                string fileContent;

                if (type == "word")
                {
                    fullPath = Path.Combine(_templatesBasePath, path);
                    if (Path.GetExtension(fullPath).ToLowerInvariant() == ".docx")
                    {
                        TempData["WarningMessage"] = "Невозможно отобразить содержимое DOCX файла в текстовом виде";
                        return RedirectToAction(nameof(Files));
                    }
                }
                else if (type == "json")
                {
                    fullPath = Path.Combine(_jsonBasePath, path);
                }
                else
                {
                    TempData["ErrorMessage"] = $"Неизвестный тип файла: {type}";
                    return RedirectToAction(nameof(Files));
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    TempData["ErrorMessage"] = $"Файл не найден: {path}";
                    return RedirectToAction(nameof(Files));
                }

                // Для JSON файлов пытаемся форматировать JSON для лучшего отображения
                if (type == "json")
                {
                    try
                    {
                        var jsonText = System.IO.File.ReadAllText(fullPath);
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonText);
                        fileContent = System.Text.Json.JsonSerializer.Serialize(
                            jsonDoc.RootElement,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                        );
                    }
                    catch
                    {
                        // Если не удалось распарсить как JSON, просто читаем как текст
                        fileContent = System.IO.File.ReadAllText(fullPath);
                    }
                }
                else
                {
                    // Пытаемся прочитать как текст с разными кодировками
                    try
                    {
                        fileContent = System.IO.File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                    }
                    catch
                    {
                        try
                        {
                            fileContent = System.IO.File.ReadAllText(fullPath, System.Text.Encoding.GetEncoding(1251));
                        }
                        catch
                        {
                            TempData["ErrorMessage"] = $"Ошибка при чтении файла: {path}";
                            return RedirectToAction(nameof(Files));
                        }
                    }
                }

                var viewModel = new FileViewModel
                {
                    FileName = Path.GetFileName(path),
                    FilePath = path,
                    FileType = type,
                    Content = fileContent
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при просмотре файла {path}");
                TempData["ErrorMessage"] = $"Ошибка при просмотре файла: {ex.Message}";
                return RedirectToAction(nameof(Files));
            }
        }

        // POST: TemplatesAdmin/SaveFile
        [HttpPost]
        public IActionResult SaveFile(string type, string path, string content)
        {
            try
            {
                string fullPath;

                if (type == "word")
                {
                    fullPath = Path.Combine(_templatesBasePath, path);
                }
                else if (type == "json")
                {
                    fullPath = Path.Combine(_jsonBasePath, path);
                }
                else
                {
                    TempData["ErrorMessage"] = $"Неизвестный тип файла: {type}";
                    return RedirectToAction(nameof(Files));
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    TempData["ErrorMessage"] = $"Файл не найден: {path}";
                    return RedirectToAction(nameof(Files));
                }

                // Создаем резервную копию
                var backupPath = fullPath + ".bak";
                System.IO.File.Copy(fullPath, backupPath, true);

                // Сохраняем изменения
                System.IO.File.WriteAllText(fullPath, content, System.Text.Encoding.UTF8);

                TempData["SuccessMessage"] = $"Файл {Path.GetFileName(path)} успешно сохранен";

                // Если это был JSON-файл, предлагаем синхронизировать шаблоны
                if (type == "json")
                {
                    TempData["InfoMessage"] = "Изменения в JSON-файле сохранены. Рекомендуется выполнить синхронизацию шаблонов.";
                }

                return RedirectToAction(nameof(ViewFile), new { type, path });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при сохранении файла {path}");
                TempData["ErrorMessage"] = $"Ошибка при сохранении файла: {ex.Message}";

                // Возвращаемся к редактированию файла с сохранением контента
                var viewModel = new FileViewModel
                {
                    FileName = Path.GetFileName(path),
                    FilePath = path,
                    FileType = type,
                    Content = content
                };

                return View("ViewFile", viewModel);
            }
        }

        // GET: TemplatesAdmin/DeleteFile
        public IActionResult DeleteFile(string type, string path)
        {
            try
            {
                string fullPath;

                if (type == "word")
                {
                    fullPath = Path.Combine(_templatesBasePath, path);
                }
                else if (type == "json")
                {
                    fullPath = Path.Combine(_jsonBasePath, path);
                }
                else
                {
                    TempData["ErrorMessage"] = $"Неизвестный тип файла: {type}";
                    return RedirectToAction(nameof(Files));
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    TempData["ErrorMessage"] = $"Файл не найден: {path}";
                    return RedirectToAction(nameof(Files));
                }

                var viewModel = new FileViewModel
                {
                    FileName = Path.GetFileName(path),
                    FilePath = path,
                    FileType = type
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при подготовке к удалению файла {path}");
                TempData["ErrorMessage"] = $"Ошибка: {ex.Message}";
                return RedirectToAction(nameof(Files));
            }
        }

        // POST: TemplatesAdmin/DeleteFileConfirmed
        [HttpPost, ActionName("DeleteFileConfirmed")]
        public IActionResult DeleteFileConfirmed(string type, string path)
        {
            try
            {
                string fullPath;

                if (type == "word")
                {
                    fullPath = Path.Combine(_templatesBasePath, path);
                }
                else if (type == "json")
                {
                    fullPath = Path.Combine(_jsonBasePath, path);
                }
                else
                {
                    TempData["ErrorMessage"] = $"Неизвестный тип файла: {type}";
                    return RedirectToAction(nameof(Files));
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    TempData["ErrorMessage"] = $"Файл не найден: {path}";
                    return RedirectToAction(nameof(Files));
                }

                // Создаем резервную копию перед удалением
                var backupPath = fullPath + ".bak";
                System.IO.File.Copy(fullPath, backupPath, true);

                // Удаляем файл
                System.IO.File.Delete(fullPath);

                TempData["SuccessMessage"] = $"Файл {Path.GetFileName(path)} успешно удален (создана резервная копия)";

                // Если это был JSON-файл, предлагаем синхронизировать шаблоны
                if (type == "json")
                {
                    TempData["InfoMessage"] = "JSON-файл удален. Рекомендуется выполнить синхронизацию шаблонов.";
                }

                return RedirectToAction(nameof(Files));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при удалении файла {path}");
                TempData["ErrorMessage"] = $"Ошибка при удалении файла: {ex.Message}";
                return RedirectToAction(nameof(Files));
            }
        }

        // GET: TemplatesAdmin/ToggleActive/5
        public async Task<IActionResult> ToggleActive(int id)
        {
            try
            {
                var template = await _templateService.GetTemplateByIdAsync(id);

                if (template == null)
                {
                    TempData["ErrorMessage"] = $"Шаблон с ID {id} не найден";
                    return RedirectToAction(nameof(Index));
                }

                // Изменяем статус активности на противоположный
                template.IsActive = !template.IsActive;

                // Сохраняем изменения
                var result = await _templateService.UpdateTemplateAsync(template);

                if (result)
                {
                    string statusMessage = template.IsActive ? "активирован" : "деактивирован";
                    TempData["SuccessMessage"] = $"Шаблон '{template.Name}' успешно {statusMessage}";
                }
                else
                {
                    TempData["ErrorMessage"] = "Не удалось изменить статус шаблона";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при изменении статуса шаблона ID {id}");
                TempData["ErrorMessage"] = $"Ошибка: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }


    }
}