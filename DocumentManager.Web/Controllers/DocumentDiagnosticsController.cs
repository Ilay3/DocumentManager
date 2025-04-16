using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Services;
using DocumentManager.Infrastructure.Utilities;
using DocumentManager.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentManager.Web.Controllers
{
    [Route("diagnostics/documents")]
    public class DocumentDiagnosticsController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IDocumentGenerationService _documentGenerationService;
        private readonly IDocumentService _documentService;
        private readonly ITemplateService _templateService;
        private readonly ILogger<DocumentDiagnosticsController> _logger;

        public DocumentDiagnosticsController(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IDocumentGenerationService documentGenerationService,
            IDocumentService documentService,
            ITemplateService templateService,
            ILogger<DocumentDiagnosticsController> logger)
        {
            _environment = environment;
            _configuration = configuration;
            _documentGenerationService = documentGenerationService;
            _documentService = documentService;
            _templateService = templateService;
            _logger = logger;
        }

        [HttpGet("test")]
        public IActionResult TestGeneration()
        {
            var viewModel = new DocumentGenerationTestViewModel
            {
                AvailableTemplates = new List<KeyValuePair<string, string>>()
            };

            try
            {
                // Get templates directory
                var templatesBasePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("TemplatesBasePath") ?? "Templates/Word"
                );

                if (Directory.Exists(templatesBasePath))
                {
                    // Find all .doc and .docx files
                    var files = Directory.GetFiles(templatesBasePath, "*.*", SearchOption.AllDirectories)
                        .Where(f => Path.GetExtension(f).ToLowerInvariant() == ".doc" ||
                               Path.GetExtension(f).ToLowerInvariant() == ".docx")
                        .ToList();

                    foreach (var file in files)
                    {
                        var relativePath = Path.GetRelativePath(templatesBasePath, file);
                        viewModel.AvailableTemplates.Add(new KeyValuePair<string, string>(
                            relativePath,
                            $"{Path.GetFileName(file)} ({relativePath})"
                        ));
                    }
                }

                // Add some default field values
                viewModel.FieldValues = new Dictionary<string, string>
                {
                    { "FactoryNumber", "TEST-123" },
                    { "ReleaseDate", DateTime.Now.ToString("yyyy-MM-dd") },
                    { "PackagingDate", DateTime.Now.ToString("yyyy-MM-dd") },
                    { "OtkName", "Тестовый ОТК" }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing test generation page");
                TempData["ErrorMessage"] = $"Ошибка: {ex.Message}";
                return View(viewModel);
            }
        }

        [HttpPost("test")]
        public async Task<IActionResult> TestGeneration(DocumentGenerationTestViewModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.SelectedTemplate))
                {
                    ModelState.AddModelError("SelectedTemplate", "Необходимо выбрать шаблон");
                    return View(model);
                }

                // Get templates and output directories
                var templatesBasePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("TemplatesBasePath") ?? "Templates/Word"
                );

                var outputBasePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("OutputBasePath") ?? "Generated"
                );

                // Ensure output directory exists
                Directory.CreateDirectory(outputBasePath);

                // Prepare the template path
                var templatePath = model.SelectedTemplate;
                var outputFileName = $"test_{Path.GetFileNameWithoutExtension(templatePath)}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(templatePath)}";

                // Generate the document
                var result = await _documentGenerationService.GenerateDocumentAsync(
                    templatePath,
                    model.FieldValues,
                    outputFileName
                );

                // Prepare download link
                var downloadPath = Path.GetRelativePath(_environment.WebRootPath, result);
                model.GeneratedFilePath = $"/{downloadPath.Replace("\\", "/")}";
                model.SuccessMessage = $"Документ успешно сгенерирован: {outputFileName}";

                // Find all existing templates to repopulate the dropdown
                var files = Directory.GetFiles(templatesBasePath, "*.*", SearchOption.AllDirectories)
                    .Where(f => Path.GetExtension(f).ToLowerInvariant() == ".doc" ||
                           Path.GetExtension(f).ToLowerInvariant() == ".docx")
                    .ToList();

                model.AvailableTemplates = files.Select(f => new KeyValuePair<string, string>(
                    Path.GetRelativePath(templatesBasePath, f),
                    $"{Path.GetFileName(f)} ({Path.GetRelativePath(templatesBasePath, f)})"
                )).ToList();

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during test generation");
                model.ErrorMessage = $"Ошибка генерации: {ex.Message}";

                // Recreate template list 
                var templatesBasePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("TemplatesBasePath") ?? "Templates/Word"
                );

                if (Directory.Exists(templatesBasePath))
                {
                    var files = Directory.GetFiles(templatesBasePath, "*.*", SearchOption.AllDirectories)
                        .Where(f => Path.GetExtension(f).ToLowerInvariant() == ".doc" ||
                               Path.GetExtension(f).ToLowerInvariant() == ".docx")
                        .ToList();

                    model.AvailableTemplates = files.Select(f => new KeyValuePair<string, string>(
                        Path.GetRelativePath(templatesBasePath, f),
                        $"{Path.GetFileName(f)} ({Path.GetRelativePath(templatesBasePath, f)})"
                    )).ToList();
                }

                return View(model);
            }
        }

        [HttpGet("analyze/{id}")]
        public async Task<IActionResult> AnalyzeTemplate(int id)
        {
            try
            {
                var template = await _templateService.GetTemplateByIdAsync(id);

                if (template == null)
                {
                    return NotFound();
                }

                var templatesBasePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("TemplatesBasePath") ?? "Templates/Word"
                );

                var templatePath = Path.Combine(templatesBasePath, template.WordTemplatePath);

                // Find the template file or a similar one
                string actualTemplatePath = templatePath;
                if (!System.IO.File.Exists(templatePath))
                {
                    var directory = Path.GetDirectoryName(templatePath);
                    var fileName = Path.GetFileNameWithoutExtension(templatePath);

                    if (Directory.Exists(directory))
                    {
                        var files = Directory.GetFiles(directory, $"{fileName}*.*");
                        if (files.Length > 0)
                        {
                            actualTemplatePath = files[0];
                        }
                        else
                        {
                            return NotFound("Template file not found");
                        }
                    }
                    else
                    {
                        return NotFound("Template directory not found");
                    }
                }

                // Analyze the template
                var extension = Path.GetExtension(actualTemplatePath).ToLowerInvariant();
                List<string> placeholders = new List<string>();

                if (extension == ".doc")
                {
                    // Use the binary handler for .doc files
                    var docHandler = new DocBinaryTemplateHandler(_logger);
                    placeholders = docHandler.FindPlaceholders(actualTemplatePath);
                }
                else
                {
                    // For .docx files, we could use DocX to find placeholders
                    // But for simplicity, we'll just read the file as XML
                    using (var docx = Xceed.Words.NET.DocX.Load(actualTemplatePath))
                    {
                        var text = docx.Text;
                        var regex = new System.Text.RegularExpressions.Regex(@"\{\{([^}]+)\}\}");
                        var matches = regex.Matches(text);

                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            if (match.Groups.Count > 1)
                            {
                                placeholders.Add(match.Groups[1].Value);
                            }
                        }
                    }
                }

                // Get the template fields
                var fields = await _templateService.GetTemplateFieldsAsync(id);

                var result = new StringBuilder();
                result.AppendLine("<h1>Template Analysis</h1>");
                result.AppendLine($"<h2>{template.Name}</h2>");

                result.AppendLine("<div class='card mb-4'>");
                result.AppendLine("<div class='card-header'>Template Information</div>");
                result.AppendLine("<div class='card-body'>");
                result.AppendLine("<dl class='row'>");
                result.AppendLine($"<dt class='col-sm-3'>Template Code:</dt><dd class='col-sm-9'>{template.Code}</dd>");
                result.AppendLine($"<dt class='col-sm-3'>Template Name:</dt><dd class='col-sm-9'>{template.Name}</dd>");
                result.AppendLine($"<dt class='col-sm-3'>Template Type:</dt><dd class='col-sm-9'>{template.Type}</dd>");
                result.AppendLine($"<dt class='col-sm-3'>Word Template Path:</dt><dd class='col-sm-9'>{template.WordTemplatePath}</dd>");
                result.AppendLine($"<dt class='col-sm-3'>Actual Template Path:</dt><dd class='col-sm-9'>{actualTemplatePath}</dd>");
                result.AppendLine($"<dt class='col-sm-3'>Template Format:</dt><dd class='col-sm-9'>{extension}</dd>");
                result.AppendLine("</dl>");
                result.AppendLine("</div>");
                result.AppendLine("</div>");

                result.AppendLine("<div class='card mb-4'>");
                result.AppendLine("<div class='card-header'>Placeholders in Template</div>");
                result.AppendLine("<div class='card-body'>");

                if (placeholders.Count > 0)
                {
                    result.AppendLine("<table class='table'>");
                    result.AppendLine("<thead><tr><th>Placeholder</th><th>Corresponding Field</th></tr></thead>");
                    result.AppendLine("<tbody>");

                    foreach (var placeholder in placeholders.Distinct())
                    {
                        var field = fields.FirstOrDefault(f => f.FieldName == placeholder);

                        result.AppendLine("<tr>");
                        result.AppendLine($"<td>{{{{ {placeholder} }}}}</td>");

                        if (field != null)
                        {
                            result.AppendLine($"<td class='text-success'>{field.FieldLabel} (Field exists)</td>");
                        }
                        else
                        {
                            result.AppendLine($"<td class='text-danger'>No corresponding field</td>");
                        }

                        result.AppendLine("</tr>");
                    }

                    result.AppendLine("</tbody>");
                    result.AppendLine("</table>");
                }
                else
                {
                    result.AppendLine("<div class='alert alert-warning'>No placeholders found in template</div>");
                }

                result.AppendLine("</div>");
                result.AppendLine("</div>");

                result.AppendLine("<div class='card mb-4'>");
                result.AppendLine("<div class='card-header'>Fields in Database</div>");
                result.AppendLine("<div class='card-body'>");

                if (fields.Any())
                {
                    result.AppendLine("<table class='table'>");
                    result.AppendLine("<thead><tr><th>Field Name</th><th>Field Label</th><th>Type</th><th>Required</th><th>Unique</th><th>Default Value</th><th>In Template</th></tr></thead>");
                    result.AppendLine("<tbody>");

                    foreach (var field in fields)
                    {
                        var inTemplate = placeholders.Contains(field.FieldName);

                        result.AppendLine("<tr>");
                        result.AppendLine($"<td>{field.FieldName}</td>");
                        result.AppendLine($"<td>{field.FieldLabel}</td>");
                        result.AppendLine($"<td>{field.FieldType}</td>");
                        result.AppendLine($"<td>{(field.IsRequired ? "Yes" : "No")}</td>");
                        result.AppendLine($"<td>{(field.IsUnique ? "Yes" : "No")}</td>");
                        result.AppendLine($"<td>{field.DefaultValue}</td>");

                        if (inTemplate)
                        {
                            result.AppendLine($"<td class='text-success'>Yes</td>");
                        }
                        else
                        {
                            result.AppendLine($"<td class='text-warning'>No</td>");
                        }

                        result.AppendLine("</tr>");
                    }

                    result.AppendLine("</tbody>");
                    result.AppendLine("</table>");
                }
                else
                {
                    result.AppendLine("<div class='alert alert-warning'>No fields defined for this template</div>");
                }

                result.AppendLine("</div>");
                result.AppendLine("</div>");

                result.AppendLine("<div class='mt-3'>");
                result.AppendLine($"<a href='{Url.Action("Details", "Templates", new { id = template.Id })}' class='btn btn-primary me-2'>View Template Details</a>");
                result.AppendLine($"<a href='{Url.Action("TestGeneration", "DocumentDiagnostics")}' class='btn btn-info'>Test Document Generation</a>");
                result.AppendLine("</div>");

                return Content(result.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing template");
                return Content($"<div class='alert alert-danger'>Error analyzing template: {ex.Message}</div>", "text/html");
            }
        }
    }

    public class DocumentGenerationTestViewModel
    {
        public string SelectedTemplate { get; set; }
        public List<KeyValuePair<string, string>> AvailableTemplates { get; set; } = new List<KeyValuePair<string, string>>();
        public Dictionary<string, string> FieldValues { get; set; } = new Dictionary<string, string>();
        public string GeneratedFilePath { get; set; }
        public string SuccessMessage { get; set; }
        public string ErrorMessage { get; set; }
    }
}