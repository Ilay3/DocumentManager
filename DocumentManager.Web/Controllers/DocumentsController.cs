﻿using DocumentManager.Core.Entities;
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Services;
using DocumentManager.Web.Helpers;
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

                // Загружаем информацию о полях документа
                var templateFields = await _templateService.GetTemplateFieldsAsync(document.DocumentTemplateId);

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
                    }).ToList(),

                    // Добавляем информацию о полях
                    DocumentFields = templateFields.Select(f => new DocumentFieldViewModel
                    {
                        Id = f.Id,
                        FieldName = f.FieldName,
                        FieldLabel = f.FieldLabel,
                        FieldType = f.FieldType,
                        IsRequired = f.IsRequired,
                        IsUnique = f.IsUnique,
                        DefaultValue = f.DefaultValue
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

        // Обновите метод Index в DocumentsController.cs
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
                var allDocuments = await _documentService.GetAllDocumentsAsync();

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

                filteredDocuments = filteredDocuments.OrderByDescending(d => d.CreatedAt);

                var totalCount = filteredDocuments.Count();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Получаем основные документы для текущей страницы
                var mainDocuments = filteredDocuments
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Создаем список для моделей представления
                var documentViewModels = new List<DocumentViewModel>();

                // Заполняем информацию для каждого основного документа
                foreach (var doc in mainDocuments)
                {
                    var viewModel = new DocumentViewModel
                    {
                        Id = doc.Id,
                        TemplateId = doc.DocumentTemplateId,
                        TemplateCode = doc.DocumentTemplate.Code,
                        TemplateName = doc.DocumentTemplate.Name,
                        FactoryNumber = doc.FactoryNumber,
                        CreatedAt = doc.CreatedAt,
                        CreatedBy = doc.CreatedBy,
                        GeneratedFilePath = doc.GeneratedFilePath,
                        IsMainDocument = true,
                        DocumentGroupId = doc.FactoryNumber
                    };

                    // Проверяем наличие связанных документов
                    bool hasRelated = await _documentService.GetRelatedDocumentsAsync(doc.Id).ContinueWith(t => t.Result.Any());
                    if (hasRelated)
                    {
                        // Для списка нам не нужно загружать все связанные документы, достаточно знать их количество
                        int relatedCount = await _documentService.GetRelatedDocumentsAsync(doc.Id).ContinueWith(t => t.Result.Count());
                        viewModel.RelatedDocuments = Enumerable.Range(0, relatedCount).Select(_ => new DocumentViewModel()).ToList();
                    }

                    documentViewModels.Add(viewModel);
                }

                var templates = await _templateService.GetAllTemplatesAsync();
                var templateViewModels = templates.Select(t => new DocumentTemplateViewModel
                {
                    Id = t.Id,
                    Code = t.Code,
                    Name = t.Name,
                    Type = t.Type,
                    IsActive = t.IsActive
                }).ToList();

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
                    IsFactoryNumberDuplicate = false, // Добавляем свойство для модального окна
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

                // Получаем все шаблоны документов
                var allTemplates = await _templateService.GetAllTemplatesAsync();

                // Используем вспомогательный класс для определения связанных шаблонов
                var relatedTemplates = DocumentRelationHelper.FindRelatedTemplates(template, allTemplates)
                    .Select(t => new DocumentTemplateViewModel
                    {
                        Id = t.Id,
                        Code = t.Code,
                        Name = t.Name,
                        Type = t.Type,
                        IsActive = t.IsActive
                    })
                    .ToList();

                _logger.LogInformation($"Найдено {relatedTemplates.Count} связанных шаблонов для {template.Type} {template.Code}");

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateForm(int templateId, Dictionary<string, string> fieldValues, List<int> selectedRelatedTemplateIds, bool confirmDuplicate = false)
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
                bool isFactoryNumberDuplicate = false;

                if (fieldValues.TryGetValue("FactoryNumber", out factoryNumber))
                {
                    var isUnique = await _documentService.IsFactoryNumberUniqueAsync(templateId, factoryNumber);

                    if (!isUnique && !confirmDuplicate)
                    {
                        isFactoryNumberDuplicate = true;
                        // Если пользователь еще не подтвердил создание дубликата, показываем модальное окно
                        // Не добавляем ошибку в ModelState, чтобы форма не показывала стандартное сообщение об ошибке
                    }
                }
                else
                {
                    ModelState.AddModelError("FactoryNumber", "Заводской номер обязателен");
                    hasValidationErrors = true;
                }

                if (!ModelState.IsValid || (hasValidationErrors || (isFactoryNumberDuplicate && !confirmDuplicate)))
                {
                    _logger.LogWarning("Ошибки валидации при создании документа");

                    var viewModel = new DocumentFormViewModel
                    {
                        TemplateId = template.Id,
                        TemplateCode = template.Code,
                        TemplateName = template.Name,
                        IsFactoryNumberDuplicate = isFactoryNumberDuplicate,
                        FactoryNumber = factoryNumber,
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

                    // Получаем связанные шаблоны
                    var relatedTemplates = (await _templateService.GetAllTemplatesAsync(includeInactive: true))
                        .Where(t => t.Id != templateId &&
                              (t.Type == "PackingList" || t.Type == "PackingInventory") &&
                              t.IsActive)
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

                _logger.LogInformation($"Создание документа для шаблона {templateId} с заводским номером {factoryNumber}. Дубликат: {confirmDuplicate}");

                // Создаем документ, игнорируя ограничение уникальности если подтверждено создание дубликата
                Document createdDocument;
                try
                {
                    // Пытаемся создать документ обычным способом
                    createdDocument = await _documentService.CreateDocumentAsync(document, fieldValues);
                }
                catch (Exception ex) when (confirmDuplicate && ex.ToString().Contains("IX_Documents_DocumentTemplateId_FactoryNumber"))
                {
                    // Если возникла ошибка уникальности и пользователь согласен создать дубликат,
                    // используем специальный формат заводского номера с пометкой дубликата
                    _logger.LogWarning($"Создание дубликата с заводским номером {factoryNumber} для шаблона {templateId}");

                    // Формируем понятное название для дубликата с меткой времени
                    var duplicateFactoryNumber = $"{factoryNumber} (Дубликат {DateTime.Now:dd.MM.yyyy HH:mm})";
                    document.FactoryNumber = duplicateFactoryNumber;

                    // Создаем документ с модифицированным номером
                    createdDocument = await _documentService.CreateDocumentAsync(document, fieldValues);

                    // Сохраняем оригинальный номер в значениях полей для отображения
                    fieldValues["OriginalFactoryNumber"] = factoryNumber;
                    fieldValues["FactoryNumber"] = duplicateFactoryNumber;
                    await _documentService.UpdateDocumentAsync(createdDocument, fieldValues);
                }

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

                        // Создаем связанный документ, учитывая возможный дубликат
                        Document createdRelatedDocument;
                        try
                        {
                            createdRelatedDocument = await _documentService.CreateDocumentAsync(relatedDocument, relatedFieldValues);
                        }
                        catch (Exception ex) when (confirmDuplicate && ex.ToString().Contains("IX_Documents_DocumentTemplateId_FactoryNumber"))
                        {
                            // Если возникла ошибка уникальности и пользователь согласен создать дубликат,
                            // используем специальный формат заводского номера с пометкой дубликата
                            _logger.LogWarning($"Создание дубликата связанного документа с заводским номером {factoryNumber} для шаблона {relatedTemplateId}");

                            // Формируем понятное название для дубликата с меткой времени
                            var duplicateFactoryNumber = $"{factoryNumber} (Дубликат {DateTime.Now:dd.MM.yyyy HH:mm})";
                            relatedDocument.FactoryNumber = duplicateFactoryNumber;

                            // Создаем документ с модифицированным номером
                            createdRelatedDocument = await _documentService.CreateDocumentAsync(relatedDocument, relatedFieldValues);

                            // Сохраняем оригинальный номер в значениях полей для отображения
                            relatedFieldValues["OriginalFactoryNumber"] = factoryNumber;
                            relatedFieldValues["FactoryNumber"] = duplicateFactoryNumber;
                            await _documentService.UpdateDocumentAsync(createdRelatedDocument, relatedFieldValues);
                        }

                        createdRelatedDocumentIds.Add(createdRelatedDocument.Id);

                        // Связываем документы
                        await _documentService.RelateDocumentsAsync(createdDocument.Id, createdRelatedDocument.Id);
                    }
                }

                // Генерируем первый документ асинхронно
                return RedirectToAction(nameof(GenerateAsync), new { id = createdDocument.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при создании документа для шаблона {templateId}");
                TempData["ErrorMessage"] = $"Ошибка при создании документа: {ex.Message}";
                return RedirectToAction(nameof(Create));
            }
        }

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

                // Загружаем информацию о полях документа
                var templateFields = await _templateService.GetTemplateFieldsAsync(document.DocumentTemplateId);

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
                    }).ToList(),

                    // Добавляем информацию о полях
                    DocumentFields = templateFields.Select(f => new DocumentFieldViewModel
                    {
                        Id = f.Id,
                        FieldName = f.FieldName,
                        FieldLabel = f.FieldLabel,
                        FieldType = f.FieldType,
                        IsRequired = f.IsRequired,
                        IsUnique = f.IsUnique,
                        DefaultValue = f.DefaultValue
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

        // Обновите метод GenerateDocument в DocumentsController.cs
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

                // Получаем связанные документы (упаковочные листы)
                var relatedDocuments = await _documentService.GetRelatedDocumentsAsync(documentId);
                int totalDocuments = 1 + (relatedDocuments?.Count() ?? 0);

                // Формируем абсолютный URL с ведущим слешем
                string downloadUrl = $"/Documents/Download/{documentId}";

                // Получаем значения полей
                var fieldValues = await _documentService.GetDocumentValuesAsync(documentId);

                _progressService.UpdateProgress(operationId, 20, "Подготовка шаблона...");

                // Генерируем основной документ
                try
                {
                    _progressService.UpdateProgress(operationId, 30, "Генерация основного документа...");

                    // Вызываем сервис генерации с собранными данными
                    var generationResult = await _documentGenerationService.GenerateDocumentAsync(documentId);
                    string filePath = generationResult.FilePath;
                    byte[] content = generationResult.Content;

                    _progressService.UpdateProgress(operationId, 50, "Сохранение основного документа...");

                    // Обновляем путь к сгенерированному файлу и содержимое
                    await _documentService.UpdateDocumentContentAsync(documentId, content, filePath);

                    // Генерируем связанные документы
                    int relatedDocsGenerated = 0;
                    if (relatedDocuments != null && relatedDocuments.Any())
                    {
                        _progressService.UpdateProgress(operationId, 60, $"Генерация связанных документов ({relatedDocuments.Count()})...");
                        int current = 0;
                        foreach (var relatedDoc in relatedDocuments)
                        {
                            current++;
                            relatedDocsGenerated++;
                            _progressService.UpdateProgress(operationId, 60 + (current * 20 / relatedDocuments.Count()),
                                $"Генерация связанного документа {current}/{relatedDocuments.Count()}...");

                            try
                            {
                                var relatedResult = await _documentGenerationService.GenerateDocumentAsync(relatedDoc.Id);
                                await _documentService.UpdateDocumentContentAsync(relatedDoc.Id, relatedResult.Content, relatedResult.FilePath);
                                _logger.LogInformation($"Успешно сгенерирован связанный документ ID {relatedDoc.Id}");
                            }
                            catch (Exception ex)
                            {
                                relatedDocsGenerated--;
                                _logger.LogError(ex, $"Ошибка при генерации связанного документа ID {relatedDoc.Id}");
                                // Продолжаем генерацию других связанных документов
                            }
                        }
                    }

                    _progressService.UpdateProgress(operationId, 90, "Завершение...");

                    // Формируем сообщение о завершении
                    string completionMessage;
                    if (relatedDocsGenerated > 0)
                    {
                        completionMessage = $"Успешно сгенерирован пакет из {relatedDocsGenerated + 1} документов";
                    }
                    else
                    {
                        completionMessage = "Документ успешно сгенерирован";
                    }

                    // Используем заранее сформированный URL
                    _progressService.CompleteOperation(operationId, completionMessage);
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

        [HttpGet]
        [Route("Documents/DownloadAll/{id}")]
        public async Task<IActionResult> DownloadAll(int id)
        {
            try
            {
                // Получаем основной документ
                var document = await _documentService.GetDocumentByIdAsync(id);

                if (document == null)
                {
                    _logger.LogWarning($"Документ с ID {id} не найден");
                    return NotFound();
                }

                // Получаем связанные документы
                var relatedDocuments = await _documentService.GetRelatedDocumentsAsync(id);

                // Создаем список всех документов (главный + связанные)
                var allDocuments = new List<Core.Entities.Document> { document };
                allDocuments.AddRange(relatedDocuments);

                // Проверяем, сгенерированы ли все документы
                if (allDocuments.Any(d => string.IsNullOrWhiteSpace(d.GeneratedFilePath)))
                {
                    TempData["WarningMessage"] = "Некоторые документы еще не сгенерированы. Необходимо сначала сгенерировать все документы.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Создаем ZIP-архив
                using (var memoryStream = new MemoryStream())
                {
                    using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                    {
                        foreach (var doc in allDocuments)
                        {
                            if (!string.IsNullOrWhiteSpace(doc.GeneratedFilePath) && System.IO.File.Exists(doc.GeneratedFilePath))
                            {
                                // Получаем содержимое файла
                                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(doc.GeneratedFilePath);

                                // Создаем безопасное имя файла
                                string originalFileName = $"{doc.DocumentTemplate.Code}_{doc.FactoryNumber}";
                                string transliteratedFileName = TransliterationHelper.Transliterate(originalFileName);
                                string safeFileName = transliteratedFileName
                                    .Replace(" ", "_")
                                    .Replace(",", "_")
                                    .Replace("\"", "_")
                                    .Replace("'", "_")
                                    .Replace(":", "_")
                                    .Replace(";", "_")
                                    .Replace("?", "_") + Path.GetExtension(doc.GeneratedFilePath);

                                // Добавляем в архив
                                var entry = archive.CreateEntry(safeFileName);
                                using (var entryStream = entry.Open())
                                {
                                    await entryStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                                }
                            }
                        }
                    }

                    memoryStream.Position = 0;

                    // Создаем имя архива
                    string zipFileName = TransliterationHelper.Transliterate($"{document.DocumentTemplate.Code}_{document.FactoryNumber}_Пакет.zip")
                        .Replace(" ", "_");

                    _logger.LogInformation($"Создан архив {zipFileName} с {allDocuments.Count} документами");

                    return File(memoryStream.ToArray(), "application/zip", zipFileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при создании пакета документов для ID {id}");
                TempData["ErrorMessage"] = $"Ошибка при создании пакета документов: {ex.Message}";
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

        [HttpGet]
        public async Task<IActionResult> GetRelatedDocuments(int id)
        {
            try
            {
                var document = await _documentService.GetDocumentByIdAsync(id);
                if (document == null)
                {
                    return NotFound();
                }

                var relatedDocuments = await _documentService.GetRelatedDocumentsAsync(id);

                if (!relatedDocuments.Any())
                {
                    return PartialView("_NoRelatedDocuments");
                }

                var viewModels = relatedDocuments.Select(d => new DocumentViewModel
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

                return PartialView("_RelatedDocuments", viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрузке связанных документов для ID {id}");
                return Content("<div class='alert alert-danger'>Ошибка при загрузке связанных документов</div>");
            }
        }
    }
}