using DocumentManager.Core.Entities;
using DocumentManager.Core.Interfaces;
using DocumentManager.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentManager.Web.Controllers
{
    [Route("generation")]
    public class DocumentGenerationController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IDocumentGenerationService _documentGenerationService;
        private readonly IDocumentService _documentService;
        private readonly ITemplateService _templateService;
        private readonly ILogger<DocumentGenerationController> _logger;

        public DocumentGenerationController(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IDocumentGenerationService documentGenerationService,
            IDocumentService documentService,
            ITemplateService templateService,
            ILogger<DocumentGenerationController> logger)
        {
            _environment = environment;
            _configuration = configuration;
            _documentGenerationService = documentGenerationService;
            _documentService = documentService;
            _templateService = templateService;
            _logger = logger;
        }

        [HttpGet("test")]
        public async Task<IActionResult> Test()
        {
            var templates = await _templateService.GetAllTemplatesAsync();

            var model = new DocumentGenerationViewModel
            {
                Templates = templates.Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name
                }).ToList(),
                FieldValues = new Dictionary<string, string>
                {
                    { "FactoryNumber", "TEST-123" },
                    { "ReleaseDate", DateTime.Now.ToString("yyyy-MM-dd") },
                    { "PackagingDate", DateTime.Now.ToString("yyyy-MM-dd") },
                    { "OtkName", "Тестовый ОТК" },
                    { "SystemDesignation", "ЭРЧМ30Т3.00.00.000-10" },
                    { "ControllerSerialNumber", "КУ-42567" },
                    { "ExecutiveDeviceSerialNumber", "ИУ-98732" },
                    { "FrequencyConverterSerialNumber_1", "ПЧ-87612" },
                    { "FrequencyConverterSerialNumber_2", "ПЧ-98124" },
                    { "PowerSupplySerialNumber", "БП-45672" },
                    { "PressureConverterSerial_16", "ПД-65983" },
                    { "PressureConverterSerial_2_5", "ПД-25478" }
                }
            };

            return View(model);
        }

        [HttpPost("test")]
        public async Task<IActionResult> Test(DocumentGenerationViewModel model)
        {
            try
            {
                if (model.SelectedTemplateId <= 0)
                {
                    ModelState.AddModelError("SelectedTemplateId", "Необходимо выбрать шаблон");

                    // Перезагружаем список шаблонов
                    var templatess = await _templateService.GetAllTemplatesAsync();
                    model.Templates = templatess.Select(t => new SelectListItem
                    {
                        Value = t.Id.ToString(),
                        Text = t.Name
                    }).ToList();

                    return View(model);
                }

                var template = await _templateService.GetTemplateByIdAsync(model.SelectedTemplateId);

                if (template == null)
                {
                    ModelState.AddModelError("SelectedTemplateId", "Выбранный шаблон не найден");
                    return View(model);
                }

                // Создаем временный документ для теста
                var document = new Document
                {
                    DocumentTemplateId = template.Id,
                    FactoryNumber = model.FieldValues.GetValueOrDefault("FactoryNumber", "TEST-" + DateTime.Now.Ticks.ToString().Substring(0, 6)),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "TestUser",
                    DocumentTemplate = template
                };

                // Генерируем документ напрямую без сохранения в БД
                var outputFileName = $"{template.Code}_{document.FactoryNumber}_{DateTime.Now:yyyyMMdd}.doc";

                var templatePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("TemplatesBasePath") ?? "Templates/Word",
                    template.WordTemplatePath
                );

                var outputBasePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("OutputBasePath") ?? "Generated"
                );

                var outputPath = Path.Combine(outputBasePath, outputFileName);

                // Генерируем документ
                var generatedFilePath = await _documentGenerationService.GenerateDocumentAsync(
                    templatePath,
                    model.FieldValues,
                    outputFileName
                );

                // Готовим относительный путь для скачивания
                var relativePath = Path.GetRelativePath(_environment.WebRootPath, generatedFilePath);
                model.GeneratedFilePath = "/" + relativePath.Replace("\\", "/");
                model.SuccessMessage = $"Документ успешно сгенерирован! Вы можете скачать его по ссылке ниже.";

                // Перезагружаем список шаблонов
                var templates = await _templateService.GetAllTemplatesAsync();
                model.Templates = templates.Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name,
                    Selected = t.Id == model.SelectedTemplateId
                }).ToList();

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации тестового документа");

                model.ErrorMessage = $"Ошибка при генерации документа: {ex.Message}";

                // Перезагружаем список шаблонов
                var templates = await _templateService.GetAllTemplatesAsync();
                model.Templates = templates.Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name,
                    Selected = t.Id == model.SelectedTemplateId
                }).ToList();

                return View(model);
            }
        }
    }

    public class DocumentGenerationViewModel
    {
        public int SelectedTemplateId { get; set; }
        public List<SelectListItem> Templates { get; set; } = new List<SelectListItem>();
        public Dictionary<string, string> FieldValues { get; set; } = new Dictionary<string, string>();
        public string GeneratedFilePath { get; set; }
        public string SuccessMessage { get; set; }
        public string ErrorMessage { get; set; }
    }
}