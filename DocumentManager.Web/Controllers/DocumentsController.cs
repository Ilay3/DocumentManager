﻿using DocumentManager.Core.Entities;
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Services;
using DocumentManager.Web.Models;
using DocumentManager.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentManager.Web.Controllers
{
    public class DocumentsController : Controller
    {
        private readonly ITemplateService _templateService;
        private readonly IDocumentService _documentService;
        private readonly IDocumentGenerationService _documentGenerationService;
        private readonly ILogger<DocumentsController> _logger;
        private readonly ProgressService _progressService;
        private readonly SimpleAuthService _authService;

        public DocumentsController(
            ITemplateService templateService,
            IDocumentService documentService,
            IDocumentGenerationService documentGenerationService,
            ProgressService progressService,
            SimpleAuthService authService,
            ILogger<DocumentsController> logger)
        {
            _templateService = templateService;
            _documentService = documentService;
            _documentGenerationService = documentGenerationService;
            _progressService = progressService;
            _authService = authService;
            _logger = logger;
        }

        public async Task<IActionResult> DetailsSidebar(int id)
        {
            try
            {
                var document = await _documentService.GetDocumentByIdAsync(id);

                if (document == null)
                {
                    return NotFound();
                }

                var fieldValues = await _documentService.GetDocumentValuesAsync(id);
                var relatedDocuments = await _documentService.GetRelatedDocumentsAsync(id);

                var viewModel = new DocumentViewModel
                {
                    Id = document.Id,
                    TemplateId = document.DocumentTemplateId,
                    TemplateCode = document.DocumentTemplate.Code,
                    TemplateName = document.DocumentTemplate.Name,
                    FactoryNumber = document.FactoryNumber,
                    CreatedAt = document.CreatedAt,
                    CreatedBy = document.CreatedBy,
                    GeneratedFilePath = document.GeneratedFilePath,
                    FieldValues = fieldValues,
                    RelatedDocuments = relatedDocuments.Select(d => new DocumentViewModel
                    {
                        Id = d.Id,
                        TemplateId = d.DocumentTemplateId,
                        TemplateCode = d.DocumentTemplate.Code,
                        TemplateName = d.DocumentTemplate.Name,
                        FactoryNumber = d.FactoryNumber,
                        CreatedAt = d.CreatedAt,
                        CreatedBy = d.CreatedBy,
                        GeneratedFilePath = d.GeneratedFilePath
                    }).ToList()
                };

                return PartialView(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении деталей документа ID {id} для боковой панели");
                return Content($"<div class='alert alert-danger'>Ошибка: {ex.Message}</div>");
            }
        }

        
        // GET: Documents
        public async Task<IActionResult> Index(
            string factoryNumber = null,
            int? templateId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string search = null,
            string viewMode = "list",
            int page = 1,
            int pageSize = 12)
        {
            try
            {
                // Get all documents
                var allDocuments = await _documentService.GetAllDocumentsAsync();

                // Filter documents
                var filteredDocuments = allDocuments.AsQueryable();

                if (!string.IsNullOrWhiteSpace(factoryNumber))
                {
                    filteredDocuments = filteredDocuments.Where(d => d.FactoryNumber.Contains(factoryNumber, StringComparison.OrdinalIgnoreCase));
                }

                if (templateId.HasValue)
                {
                    filteredDocuments = filteredDocuments.Where(d => d.DocumentTemplateId == templateId.Value);
                }

                if (dateFrom.HasValue)
                {
                    filteredDocuments = filteredDocuments.Where(d => d.CreatedAt >= dateFrom.Value);
                }

                if (dateTo.HasValue)
                {
                    // Add one day to include the entire dateTo day
                    var dateToEnd = dateTo.Value.AddDays(1);
                    filteredDocuments = filteredDocuments.Where(d => d.CreatedAt < dateToEnd);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    filteredDocuments = filteredDocuments.Where(d =>
                        d.FactoryNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        d.DocumentTemplate.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        d.CreatedBy.Contains(search, StringComparison.OrdinalIgnoreCase));
                }

                // Order by creation date (newest first)
                filteredDocuments = filteredDocuments.OrderByDescending(d => d.CreatedAt);

                // Get total count for pagination
                var totalCount = filteredDocuments.Count();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
                filteredDocuments = filteredDocuments
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize);

                // Map to view models
                var documentViewModels = filteredDocuments.Select(d => new DocumentViewModel
                {
                    Id = d.Id,
                    TemplateId = d.DocumentTemplateId,
                    TemplateCode = d.DocumentTemplate.Code,
                    TemplateName = d.DocumentTemplate.Name,
                    FactoryNumber = d.FactoryNumber,
                    CreatedAt = d.CreatedAt,
                    CreatedBy = d.CreatedBy,
                    GeneratedFilePath = d.GeneratedFilePath
                }).ToList();

                // Get all templates for the filter dropdown
                var templates = await _templateService.GetAllTemplatesAsync();
                var templateViewModels = templates.Select(t => new DocumentTemplateViewModel
                {
                    Id = t.Id,
                    Code = t.Code,
                    Name = t.Name,
                    Type = t.Type,
                    IsActive = t.IsActive
                }).ToList();

                // Set ViewBag for filtering and pagination
                ViewBag.FactoryNumber = factoryNumber;
                ViewBag.TemplateId = templateId;
                ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
                ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
                ViewBag.Search = search;
                ViewBag.ViewMode = viewMode;
                ViewBag.Templates = templateViewModels;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalItems = totalCount;
                ViewBag.StartItem = Math.Min(totalCount, (page - 1) * pageSize + 1);
                ViewBag.EndItem = Math.Min(totalCount, page * pageSize);

                return View(documentViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка документов");
                TempData["ErrorMessage"] = $"Ошибка при получении списка документов: {ex.Message}";
                return View(new List<DocumentViewModel>());
            }
        }


        // GET: Documents/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                var templates = await _templateService.GetAllTemplatesAsync();

                var viewModels = templates.Select(t => new DocumentTemplateViewModel
                {
                    Id = t.Id,
                    Code = t.Code,
                    Name = t.Name,
                    Type = t.Type,
                    IsActive = t.IsActive
                }).ToList();

                return View(viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке шаблонов для создания документа");
                TempData["ErrorMessage"] = $"Ошибка при загрузке шаблонов: {ex.Message}";
                return View(new List<DocumentTemplateViewModel>());
            }
        }

        // GET: Documents/CreateForm/5
        public async Task<IActionResult> CreateForm(int id)
        {
            try
            {
                var template = await _templateService.GetTemplateByIdAsync(id);

                if (template == null)
                {
                    _logger.LogWarning($"Шаблон с ID {id} не найден");
                    return NotFound();
                }

                var fields = await _templateService.GetTemplateFieldsAsync(id);

                var viewModel = new DocumentFormViewModel
                {
                    TemplateId = template.Id,
                    TemplateCode = template.Code,
                    TemplateName = template.Name,
                    Fields = fields.Select(f => new DocumentFieldViewModel
                    {
                        Id = f.Id,
                        FieldName = f.FieldName,
                        FieldLabel = f.FieldLabel,
                        FieldType = f.FieldType,
                        IsRequired = f.IsRequired,
                        IsUnique = f.IsUnique,
                        DefaultValue = f.DefaultValue,
                        Value = f.DefaultValue
                    }).ToList()
                };

                // Получаем связанные шаблоны (упаковочные листы)
                var relatedTemplates = (await _templateService.GetAllTemplatesAsync())
                    .Where(t => t.Id != id && t.Type == "PackingList")
                    .Select(t => new DocumentTemplateViewModel
                    {
                        Id = t.Id,
                        Code = t.Code,
                        Name = t.Name,
                        Type = t.Type,
                        IsActive = t.IsActive
                    }).ToList();

                viewModel.RelatedTemplates = relatedTemplates;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрузке формы создания документа для шаблона ID {id}");
                TempData["ErrorMessage"] = $"Ошибка при загрузке формы: {ex.Message}";
                return RedirectToAction(nameof(Create));
            }
        }

        // POST: Documents/CreateForm - Обновление для сохранения текущего пользователя
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateForm(int templateId, Dictionary<string, string> fieldValues, List<int> selectedRelatedTemplateIds)
        {
            try
            {
                var template = await _templateService.GetTemplateByIdAsync(templateId);

                if (template == null)
                {
                    _logger.LogWarning($"Шаблон с ID {templateId} не найден");
                    return NotFound();
                }

                var fields = await _templateService.GetTemplateFieldsAsync(templateId);

                // Проверяем обязательные поля
                bool hasValidationErrors = false;
                foreach (var field in fields.Where(f => f.IsRequired))
                {
                    if (!fieldValues.ContainsKey(field.FieldName) || string.IsNullOrWhiteSpace(fieldValues[field.FieldName]))
                    {
                        ModelState.AddModelError(field.FieldName, $"Поле '{field.FieldLabel}' обязательно для заполнения");
                        hasValidationErrors = true;
                    }
                }

                // Проверяем уникальность заводского номера
                string factoryNumber = null;
                if (fieldValues.TryGetValue("FactoryNumber", out factoryNumber))
                {
                    var isUnique = await _documentService.IsFactoryNumberUniqueAsync(templateId, factoryNumber);

                    if (!isUnique)
                    {
                        ModelState.AddModelError("FactoryNumber", "Заводской номер уже существует");
                        hasValidationErrors = true;
                    }
                }
                else
                {
                    ModelState.AddModelError("FactoryNumber", "Заводской номер обязателен");
                    hasValidationErrors = true;
                }

                if (!ModelState.IsValid || hasValidationErrors)
                {
                    _logger.LogWarning("Ошибки валидации при создании документа");

                    var viewModel = new DocumentFormViewModel
                    {
                        TemplateId = template.Id,
                        TemplateCode = template.Code,
                        TemplateName = template.Name,
                        Fields = fields.Select(f => new DocumentFieldViewModel
                        {
                            Id = f.Id,
                            FieldName = f.FieldName,
                            FieldLabel = f.FieldLabel,
                            FieldType = f.FieldType,
                            IsRequired = f.IsRequired,
                            IsUnique = f.IsUnique,
                            DefaultValue = f.DefaultValue,
                            Value = fieldValues.TryGetValue(f.FieldName, out var value) ? value : f.DefaultValue
                        }).ToList(),
                        SelectedRelatedTemplateIds = selectedRelatedTemplateIds
                    };

                    // Получаем связанные шаблоны (упаковочные листы)
                    var relatedTemplates = (await _templateService.GetAllTemplatesAsync())
                        .Where(t => t.Id != templateId && t.Type == "PackingList")
                        .Select(t => new DocumentTemplateViewModel
                        {
                            Id = t.Id,
                            Code = t.Code,
                            Name = t.Name,
                            Type = t.Type,
                            IsActive = t.IsActive
                        }).ToList();

                    viewModel.RelatedTemplates = relatedTemplates;

                    return View(viewModel);
                }

                // Получаем текущего пользователя
                string createdBy = _authService.GetCurrentUser() ?? "Система";

                // Создаем документ
                var document = new Document
                {
                    DocumentTemplateId = templateId,
                    FactoryNumber = factoryNumber,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = createdBy
                };

                _logger.LogInformation($"Создание документа для шаблона {templateId} с заводским номером {factoryNumber}");
                await _documentService.CreateDocumentAsync(document, fieldValues);

                // Создаем связанные документы (упаковочные листы)
                List<int> createdRelatedDocumentIds = new List<int>();
                foreach (var relatedTemplateId in selectedRelatedTemplateIds)
                {
                    var relatedTemplate = await _templateService.GetTemplateByIdAsync(relatedTemplateId);

                    if (relatedTemplate != null)
                    {
                        var relatedFieldValues = new Dictionary<string, string>();

                        // Копируем значения полей из основного документа в связанный
                        foreach (var field in await _templateService.GetTemplateFieldsAsync(relatedTemplateId))
                        {
                            if (fieldValues.TryGetValue(field.FieldName, out var value))
                            {
                                relatedFieldValues[field.FieldName] = value;
                            }
                            else if (!string.IsNullOrWhiteSpace(field.DefaultValue))
                            {
                                relatedFieldValues[field.FieldName] = field.DefaultValue;
                            }
                        }

                        var relatedDocument = new Document
                        {
                            DocumentTemplateId = relatedTemplateId,
                            FactoryNumber = factoryNumber,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = createdBy
                        };

                        _logger.LogInformation($"Создание связанного документа для шаблона {relatedTemplateId}");
                        await _documentService.CreateDocumentAsync(relatedDocument, relatedFieldValues);
                        createdRelatedDocumentIds.Add(relatedDocument.Id);

                        // Связываем документы
                        await _documentService.RelateDocumentsAsync(document.Id, relatedDocument.Id);
                    }
                }

                // Генерируем первый документ асинхронно
                return RedirectToAction(nameof(GenerateAsync), new { id = document.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при создании документа для шаблона {templateId}");
                TempData["ErrorMessage"] = $"Ошибка при создании документа: {ex.Message}";
                return RedirectToAction(nameof(Create));
            }
        }
    


        // GET: Documents/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var document = await _documentService.GetDocumentByIdAsync(id);

                if (document == null)
                {
                    _logger.LogWarning($"Документ с ID {id} не найден");
                    return NotFound();
                }

                var fieldValues = await _documentService.GetDocumentValuesAsync(id);
                var relatedDocuments = await _documentService.GetRelatedDocumentsAsync(id);

                var viewModel = new DocumentViewModel
                {
                    Id = document.Id,
                    TemplateId = document.DocumentTemplateId,
                    TemplateCode = document.DocumentTemplate.Code,
                    TemplateName = document.DocumentTemplate.Name,
                    FactoryNumber = document.FactoryNumber,
                    CreatedAt = document.CreatedAt,
                    CreatedBy = document.CreatedBy,
                    GeneratedFilePath = document.GeneratedFilePath,
                    FieldValues = fieldValues,
                    RelatedDocuments = relatedDocuments.Select(d => new DocumentViewModel
                    {
                        Id = d.Id,
                        TemplateId = d.DocumentTemplateId,
                        TemplateCode = d.DocumentTemplate.Code,
                        TemplateName = d.DocumentTemplate.Name,
                        FactoryNumber = d.FactoryNumber,
                        CreatedAt = d.CreatedAt,
                        CreatedBy = d.CreatedBy,
                        GeneratedFilePath = d.GeneratedFilePath
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при просмотре деталей документа ID {id}");
                TempData["ErrorMessage"] = $"Ошибка при загрузке деталей документа: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        [Route("Documents/GenerateAsync/{id}")]
        public IActionResult GenerateAsync(int id)
        {
            try
            {
                // Создаем операцию отслеживания прогресса
                var operationId = _progressService.CreateOperation();

                // Запускаем асинхронную генерацию документа в фоновом режиме
                Task.Run(async () => await GenerateDocument(id, operationId));

                // Возвращаем страницу с прогресс-баром
                var viewModel = new GenerateProgressViewModel
                {
                    DocumentId = id,
                    OperationId = operationId
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при инициализации асинхронной генерации документа ID {id}");
                TempData["ErrorMessage"] = $"Ошибка при генерации документа: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }


        private async Task GenerateDocument(int documentId, string operationId)
        {
            try
            {
                // Обновляем прогресс
                _progressService.UpdateProgress(operationId, 10, "Загрузка документа из базы данных...");

                // Создаем копию всех необходимых данных до начала фоновой обработки
                var document = await _documentService.GetDocumentByIdAsync(documentId);
                if (document == null)
                {
                    _progressService.CompleteOperationWithError(operationId, $"Документ с ID {documentId} не найден");
                    return;
                }

                // Формируем абсолютный URL с ведущим слешем, чтобы гарантировать правильное формирование пути
                string downloadUrl = $"/Documents/Download/{documentId}";

                // Получаем значения полей
                var fieldValues = await _documentService.GetDocumentValuesAsync(documentId);

                _progressService.UpdateProgress(operationId, 20, "Подготовка шаблона...");

                // Генерируем документ
                try
                {
                    _progressService.UpdateProgress(operationId, 30, "Генерация документа...");

                    // Вызываем сервис генерации с собранными данными
                    var generationResult = await _documentGenerationService.GenerateDocumentAsync(documentId);
                    string filePath = generationResult.FilePath;
                    byte[] content = generationResult.Content;

                    _progressService.UpdateProgress(operationId, 70, "Сохранение документа...");

                    // Обновляем путь к сгенерированному файлу и содержимое
                    await _documentService.UpdateDocumentContentAsync(documentId, content, filePath);

                    _progressService.UpdateProgress(operationId, 90, "Завершение...");

                    // Используем заранее сформированный URL
                    _progressService.CompleteOperation(operationId, "Документ успешно сгенерирован");
                    _progressService.GetProgress(operationId).Result = downloadUrl;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка при генерации документа ID {documentId}");
                    _progressService.CompleteOperationWithError(operationId, ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Неожиданная ошибка при генерации документа ID {documentId}");
                _progressService.CompleteOperationWithError(operationId, "Произошла неожиданная ошибка");
            }
        }

        public static class TransliterationHelper
        {
            private static readonly Dictionary<char, string> transliterationMap = new Dictionary<char, string>
        {
            {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "g"}, {'д', "d"}, {'е', "e"}, {'ё', "yo"},
            {'ж', "zh"}, {'з', "z"}, {'и', "i"}, {'й', "y"}, {'к', "k"}, {'л', "l"}, {'м', "m"},
            {'н', "n"}, {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"}, {'у', "u"},
            {'ф', "f"}, {'х', "kh"}, {'ц', "ts"}, {'ч', "ch"}, {'ш', "sh"}, {'щ', "sch"}, {'ъ', ""},
            {'ы', "y"}, {'ь', ""}, {'э', "e"}, {'ю', "yu"}, {'я', "ya"},
            {'А', "A"}, {'Б', "B"}, {'В', "V"}, {'Г', "G"}, {'Д', "D"}, {'Е', "E"}, {'Ё', "Yo"},
            {'Ж', "Zh"}, {'З', "Z"}, {'И', "I"}, {'Й', "Y"}, {'К', "K"}, {'Л', "L"}, {'М', "M"},
            {'Н', "N"}, {'О', "O"}, {'П', "P"}, {'Р', "R"}, {'С', "S"}, {'Т', "T"}, {'У', "U"},
            {'Ф', "F"}, {'Х', "Kh"}, {'Ц', "Ts"}, {'Ч', "Ch"}, {'Ш', "Sh"}, {'Щ', "Sch"}, {'Ъ', ""},
            {'Ы', "Y"}, {'Ь', ""}, {'Э', "E"}, {'Ю', "Yu"}, {'Я', "Ya"}
        };

            /// <summary>
            /// Транслитерирует строку с русского на английский
            /// </summary>
            public static string Transliterate(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return text;

                var result = new System.Text.StringBuilder();

                foreach (char c in text)
                {
                    // Если символ есть в словаре транслитерации, используем его
                    if (transliterationMap.TryGetValue(c, out string translit))
                    {
                        result.Append(translit);
                    }
                    // Для латинских букв, цифр и некоторых символов оставляем как есть
                    else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
                             c == ' ' || c == '_' || c == '-' || c == '.')
                    {
                        result.Append(c);
                    }
                    // Заменяем другие символы на подчеркивание
                    else
                    {
                        result.Append('_');
                    }
                }

                return result.ToString();
            }
        }

        public async Task<IActionResult> Print(int id)
        {
            try
            {
                var document = await _documentService.GetDocumentByIdAsync(id);

                if (document == null)
                {
                    _logger.LogWarning($"Документ с ID {id} не найден");
                    return NotFound();
                }

                byte[] fileBytes;
                string contentType;

                if (document.DocumentContent != null && document.DocumentContent.Length > 0)
                {
                    _logger.LogInformation($"Печать документа ID {id} из содержимого в БД");
                    fileBytes = document.DocumentContent;
                }
                else if (!string.IsNullOrWhiteSpace(document.GeneratedFilePath) && System.IO.File.Exists(document.GeneratedFilePath))
                {
                    _logger.LogInformation($"Печать документа ID {id} из файла на диске");
                    fileBytes = await System.IO.File.ReadAllBytesAsync(document.GeneratedFilePath);
                }
                else
                {
                    _logger.LogInformation($"Генерация документа ID {id} для печати");
                    var (filePath, content) = await _documentGenerationService.GenerateDocumentAsync(id);
                    fileBytes = content;

                    // Обновляем документ в базе данных
                    await _documentService.UpdateDocumentContentAsync(id, content, filePath);
                }

                // Проверка, что мы действительно получили содержимое файла
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    _logger.LogError($"Содержимое документа пустое для ID {id}");
                    TempData["ErrorMessage"] = "Не удалось получить содержимое документа. Пожалуйста, попробуйте сгенерировать документ заново.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Определяем тип контента
                var extension = Path.GetExtension(document.GeneratedFilePath ?? ".doc").ToLowerInvariant();

                if (extension == ".docx")
                {
                    contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                }
                else
                {
                    contentType = "application/msword";
                }

                // Создаем безопасное имя файла только из латинских букв, цифр и безопасных символов
                string safeFileName = $"document_{id}_{DateTime.Now:yyyyMMdd}{extension}";

                _logger.LogInformation($"Отправка файла на печать: {safeFileName}, размер: {fileBytes.Length} байт");

                // Установка заголовка для открытия файла в браузере вместо скачивания
                Response.Headers.Add("Content-Disposition", "inline");

                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при печати документа ID {id}");
                TempData["ErrorMessage"] = $"Ошибка при печати документа: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpGet]
        [Route("Documents/Download/{id}")]
        // GET: Documents/Download/5
        public async Task<IActionResult> Download(int id)
        {
            try
            {
                // Получаем документ без загрузки содержимого
                var document = await _documentService.GetDocumentByIdAsync(id);

                if (document == null)
                {
                    _logger.LogWarning($"Документ с ID {id} не найден");
                    return NotFound();
                }

                byte[] fileBytes;
                string contentType;

                if (!string.IsNullOrWhiteSpace(document.GeneratedFilePath) && System.IO.File.Exists(document.GeneratedFilePath))
                {
                    _logger.LogInformation($"Скачивание документа ID {id} из файла на диске: {document.GeneratedFilePath}");
                    fileBytes = await System.IO.File.ReadAllBytesAsync(document.GeneratedFilePath);
                }
                else
                {
                    _logger.LogInformation($"Генерация документа ID {id} для скачивания");
                    var (filePath, content) = await _documentGenerationService.GenerateDocumentAsync(id);
                    fileBytes = content;

                    // Сохраняем сгенерированное содержимое
                    if (content != null && content.Length > 0)
                    {
                        await _documentService.UpdateDocumentContentAsync(id, content, filePath);
                    }
                }

                // Проверка на пустое содержимое
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    _logger.LogError($"Содержимое документа пустое для ID {id}");
                    TempData["ErrorMessage"] = "Не удалось получить содержимое документа. Пожалуйста, попробуйте сгенерировать документ заново.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Определение типа контента и имени файла
                var extension = Path.GetExtension(document.GeneratedFilePath ?? ".doc").ToLowerInvariant();

                if (extension == ".docx")
                {
                    contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                }
                else
                {
                    contentType = "application/msword";
                }

                // Создание имени файла с транслитерацией русских символов
                string originalFileName = $"{document.DocumentTemplate.Code}_{document.FactoryNumber}";
                string transliteratedFileName = TransliterationHelper.Transliterate(originalFileName);

                // Обработка специальных символов в имени файла
                string safeFileName = transliteratedFileName
                    .Replace(" ", "_")
                    .Replace(",", "_")
                    .Replace("\"", "_")
                    .Replace("'", "_")
                    .Replace(":", "_")
                    .Replace(";", "_")
                    .Replace("?", "_") + extension;

                _logger.LogInformation($"Отправка файла клиенту: {safeFileName}, размер: {fileBytes.Length} байт");

                // Установка правильных заголовков для скачивания
                Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{safeFileName}\"");

                // Возвращаем файл клиенту
                return File(fileBytes, contentType, safeFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при скачивании документа ID {id}");
                TempData["ErrorMessage"] = $"Ошибка при скачивании документа: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }


        // GET: Documents/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var document = await _documentService.GetDocumentByIdAsync(id);

                if (document == null)
                {
                    _logger.LogWarning($"Документ с ID {id} не найден для удаления");
                    return NotFound();
                }

                return View(new DocumentViewModel
                {
                    Id = document.Id,
                    TemplateId = document.DocumentTemplateId,
                    TemplateCode = document.DocumentTemplate.Code,
                    TemplateName = document.DocumentTemplate.Name,
                    FactoryNumber = document.FactoryNumber,
                    CreatedAt = document.CreatedAt,
                    CreatedBy = document.CreatedBy
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при отображении страницы удаления документа ID {id}");
                TempData["ErrorMessage"] = $"Ошибка при загрузке страницы удаления: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Documents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                _logger.LogInformation($"Удаление документа ID {id}");
                var success = await _documentService.DeleteDocumentAsync(id);

                if (success)
                {
                    TempData["SuccessMessage"] = "Документ успешно удален";
                }
                else
                {
                    TempData["WarningMessage"] = "Не удалось удалить документ";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при удалении документа ID {id}");
                TempData["ErrorMessage"] = $"Ошибка при удалении документа: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}