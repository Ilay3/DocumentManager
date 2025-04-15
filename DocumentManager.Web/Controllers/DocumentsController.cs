using DocumentManager.Core.Entities;
using DocumentManager.Core.Interfaces;
using DocumentManager.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManager.Web.Controllers
{
    public class DocumentsController : Controller
    {
        private readonly ITemplateService _templateService;
        private readonly IDocumentService _documentService;
        private readonly IDocumentGenerationService _documentGenerationService;
        private readonly IJsonSchemaService _jsonSchemaService;

        public DocumentsController(
            ITemplateService templateService,
            IDocumentService documentService,
            IDocumentGenerationService documentGenerationService,
            IJsonSchemaService jsonSchemaService)
        {
            _templateService = templateService;
            _documentService = documentService;
            _documentGenerationService = documentGenerationService;
            _jsonSchemaService = jsonSchemaService;
        }

        // GET: Documents
        public async Task<IActionResult> Index()
        {
            var documents = await _documentService.GetAllDocumentsAsync();

            var viewModels = documents.Select(d => new DocumentViewModel
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

            return View(viewModels);
        }

        // GET: Documents/Create
        public async Task<IActionResult> Create()
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

        // GET: Documents/CreateForm/5
        public async Task<IActionResult> CreateForm(int id)
        {
            var template = await _templateService.GetTemplateByIdAsync(id);

            if (template == null)
            {
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

        // POST: Documents/CreateForm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateForm(int templateId, Dictionary<string, string> fieldValues, List<int> selectedRelatedTemplateIds)
        {
            var template = await _templateService.GetTemplateByIdAsync(templateId);

            if (template == null)
            {
                return NotFound();
            }

            var fields = await _templateService.GetTemplateFieldsAsync(templateId);

            // Проверяем обязательные поля
            foreach (var field in fields.Where(f => f.IsRequired))
            {
                if (!fieldValues.ContainsKey(field.FieldName) || string.IsNullOrWhiteSpace(fieldValues[field.FieldName]))
                {
                    ModelState.AddModelError(field.FieldName, $"Поле '{field.FieldLabel}' обязательно для заполнения");
                }
            }

            // Проверяем уникальность заводского номера
            if (fieldValues.TryGetValue("FactoryNumber", out var factoryNumber))
            {
                var isUnique = await _documentService.IsFactoryNumberUniqueAsync(templateId, factoryNumber);

                if (!isUnique)
                {
                    ModelState.AddModelError("FactoryNumber", "Заводской номер уже существует");
                }
            }

            if (!ModelState.IsValid)
            {
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

                return View(viewModel);
            }

            // Создаем документ
            var document = new Document
            {
                DocumentTemplateId = templateId,
                FactoryNumber = factoryNumber,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Admin" // TODO: Заменить на авторизованного пользователя
            };

            await _documentService.CreateDocumentAsync(document, fieldValues);

            // Создаем связанные документы (упаковочные листы)
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
                        CreatedBy = "Admin" // TODO: Заменить на авторизованного пользователя
                    };

                    await _documentService.CreateDocumentAsync(relatedDocument, relatedFieldValues);

                    // Связываем документы
                    await _documentService.RelateDocumentsAsync(document.Id, relatedDocument.Id);
                }
            }

            // Генерируем документы
            await _documentGenerationService.GenerateRelatedDocumentsAsync(document.Id);

            return RedirectToAction(nameof(Index));
        }

        // GET: Documents/Details/5
        public async Task<IActionResult> Details(int id)
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

            return View(viewModel);
        }

        // GET: Documents/Generate/5
        public async Task<IActionResult> Generate(int id)
        {
            var document = await _documentService.GetDocumentByIdAsync(id);

            if (document == null)
            {
                return NotFound();
            }

            var filePath = await _documentGenerationService.GenerateDocumentAsync(id);

            // Обновляем путь к сгенерированному файлу
            document.GeneratedFilePath = filePath;
            await _documentService.UpdateDocumentAsync(document, await _documentService.GetDocumentValuesAsync(id));

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Documents/Download/5
        public async Task<IActionResult> Download(int id)
        {
            var document = await _documentService.GetDocumentByIdAsync(id);

            if (document == null || string.IsNullOrWhiteSpace(document.GeneratedFilePath))
            {
                return NotFound();
            }

            var fileName = System.IO.Path.GetFileName(document.GeneratedFilePath);
            var fileBytes = System.IO.File.ReadAllBytes(document.GeneratedFilePath);

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }

        // GET: Documents/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var document = await _documentService.GetDocumentByIdAsync(id);

            if (document == null)
            {
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

        // POST: Documents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _documentService.DeleteDocumentAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }

}
