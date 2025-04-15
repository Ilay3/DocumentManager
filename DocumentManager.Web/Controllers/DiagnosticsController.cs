// DocumentManager.Web/Controllers/DiagnosticsController.cs
using DocumentManager.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DocumentManager.Web.Controllers
{
    public class DiagnosticsController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ITemplateService _templateService;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ITemplateService templateService,
            ILogger<DiagnosticsController> logger)
        {
            _environment = environment;
            _configuration = configuration;
            _templateService = templateService;
            _logger = logger;
        }

        // GET: /Diagnostics
        public async Task<IActionResult> Index()
        {
            var result = new StringBuilder();

            try
            {
                // Информация о путях
                result.AppendLine("<h2>Пути</h2>");
                result.AppendLine("<pre>");
                result.AppendLine($"WebRootPath: {_environment.WebRootPath}");
                result.AppendLine($"ContentRootPath: {_environment.ContentRootPath}");

                var templatesBasePath = _configuration.GetValue<string>("TemplatesBasePath");
                var jsonBasePath = _configuration.GetValue<string>("JsonBasePath");

                result.AppendLine($"TemplatesBasePath из конфигурации: {templatesBasePath}");
                result.AppendLine($"JsonBasePath из конфигурации: {jsonBasePath}");

                // Полные пути
                var fullTemplatesPath = Path.Combine(_environment.WebRootPath, templatesBasePath ?? "Templates/Word");
                var fullJsonPath = Path.Combine(_environment.WebRootPath, jsonBasePath ?? "Templates/Json");

                result.AppendLine($"Полный путь к шаблонам Word: {fullTemplatesPath}");
                result.AppendLine($"Полный путь к JSON-файлам: {fullJsonPath}");
                result.AppendLine("</pre>");

                // Проверка наличия директорий
                result.AppendLine("<h2>Проверка директорий</h2>");
                result.AppendLine("<pre>");
                result.AppendLine($"Директория шаблонов Word существует: {Directory.Exists(fullTemplatesPath)}");
                result.AppendLine($"Директория JSON-файлов существует: {Directory.Exists(fullJsonPath)}");
                result.AppendLine("</pre>");

                // Список файлов
                result.AppendLine("<h2>Файлы шаблонов Word</h2>");
                result.AppendLine("<ul>");
                if (Directory.Exists(fullTemplatesPath))
                {
                    var wordFiles = Directory.GetFiles(fullTemplatesPath, "*.doc*", SearchOption.AllDirectories);
                    foreach (var file in wordFiles)
                    {
                        result.AppendLine($"<li>{Path.GetRelativePath(fullTemplatesPath, file)}</li>");
                    }
                }
                result.AppendLine("</ul>");

                result.AppendLine("<h2>Файлы JSON</h2>");
                result.AppendLine("<ul>");
                if (Directory.Exists(fullJsonPath))
                {
                    var jsonFiles = Directory.GetFiles(fullJsonPath, "*.json", SearchOption.AllDirectories);
                    foreach (var file in jsonFiles)
                    {
                        result.AppendLine($"<li>{Path.GetRelativePath(fullJsonPath, file)}</li>");
                    }
                }
                result.AppendLine("</ul>");

                // Шаблоны в базе данных
                result.AppendLine("<h2>Шаблоны в базе данных</h2>");
                result.AppendLine("<table border='1' cellpadding='5'>");
                result.AppendLine("<tr><th>ID</th><th>Код</th><th>Имя</th><th>Тип</th><th>Путь к Word</th><th>Путь к JSON</th></tr>");

                var templates = await _templateService.GetAllTemplatesAsync();
                foreach (var template in templates)
                {
                    result.AppendLine("<tr>");
                    result.AppendLine($"<td>{template.Id}</td>");
                    result.AppendLine($"<td>{template.Code}</td>");
                    result.AppendLine($"<td>{template.Name}</td>");
                    result.AppendLine($"<td>{template.Type}</td>");
                    result.AppendLine($"<td>{template.WordTemplatePath}</td>");
                    result.AppendLine($"<td>{template.JsonSchemaPath}</td>");
                    result.AppendLine("</tr>");
                }

                result.AppendLine("</table>");

                // Если шаблонов нет, покажем кнопку для инициализации
                if (!templates.Any())
                {
                    result.AppendLine("<h2>Действия</h2>");
                    result.AppendLine("<a href='/Admin/Initialize' class='btn btn-primary'>Инициализировать шаблоны</a>");
                }
            }
            catch (Exception ex)
            {
                result.AppendLine("<h2>Ошибка</h2>");
                result.AppendLine("<pre>");
                result.AppendLine(ex.ToString());
                result.AppendLine("</pre>");
            }

            return Content(result.ToString(), "text/html");
        }
    }
}