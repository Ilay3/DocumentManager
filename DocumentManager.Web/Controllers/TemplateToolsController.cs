
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;



/// template - tools / create - simple - templates - для создания улучшенных текстовых шаблонов
///template-tools/validate - для проверки соответствия шаблонов и полей в JSON-файлах



namespace DocumentManager.Web.Controllers
{
    [Route("template-tools")]
    public class TemplateToolsController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TemplateToolsController> _logger;

        public TemplateToolsController(
            IWebHostEnvironment environment,
            ILogger<TemplateToolsController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("validate")]
        public IActionResult ValidateTemplates()
        {
            var result = new StringBuilder();
            result.AppendLine("<!DOCTYPE html>");
            result.AppendLine("<html><head>");
            result.AppendLine("<meta charset='utf-8'>");
            result.AppendLine("<title>Проверка шаблонов документов</title>");
            result.AppendLine("<link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'>");
            result.AppendLine("</head><body>");
            result.AppendLine("<div class='container mt-4'>");
            result.AppendLine("<h1>Проверка соответствия шаблонов и JSON-моделей</h1>");

            try
            {
                // Получаем пути к директориям
                var templatesDir = Path.Combine(_environment.WebRootPath, "Templates", "Word");
                var jsonDir = Path.Combine(_environment.WebRootPath, "Templates", "Json");

                // Проверяем существование директорий
                if (!Directory.Exists(templatesDir))
                {
                    result.AppendLine("<div class='alert alert-danger'>Директория шаблонов Word не найдена</div>");
                    return Content(result.ToString(), "text/html");
                }

                if (!Directory.Exists(jsonDir))
                {
                    result.AppendLine("<div class='alert alert-danger'>Директория JSON-файлов не найдена</div>");
                    return Content(result.ToString(), "text/html");
                }

                // Получаем все JSON-файлы
                var jsonFiles = Directory.GetFiles(jsonDir, "*.json", SearchOption.AllDirectories);

                result.AppendLine($"<div class='alert alert-info'>Найдено {jsonFiles.Length} JSON-файлов</div>");
                result.AppendLine("<table class='table table-striped'>");
                result.AppendLine("<thead><tr>");
                result.AppendLine("<th>JSON-файл</th>");
                result.AppendLine("<th>Шаблон Word</th>");
                result.AppendLine("<th>Статус</th>");
                result.AppendLine("<th>Поля в JSON</th>");
                result.AppendLine("<th>Плейсхолдеры в шаблоне</th>");
                result.AppendLine("<th>Отсутствующие поля</th>");
                result.AppendLine("</tr></thead><tbody>");

                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        var relativePath = Path.GetRelativePath(jsonDir, jsonFile);
                        result.AppendLine("<tr>");
                        result.AppendLine($"<td>{relativePath}</td>");

                        // Читаем JSON-файл
                        var jsonContent = System.IO.File.ReadAllText(jsonFile);
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                        var root = jsonDoc.RootElement;

                        // Получаем путь к шаблону
                        string templatePath = null;
                        if (root.TryGetProperty("templatePath", out var templatePathElement))
                        {
                            templatePath = templatePathElement.GetString();
                        }

                        var fullTemplatePath = templatePath != null ? Path.Combine(_environment.WebRootPath, templatePath) : null;

                        if (string.IsNullOrEmpty(templatePath))
                        {
                            result.AppendLine("<td>-</td>");
                            result.AppendLine("<td class='text-danger'>Путь к шаблону не указан</td>");
                            result.AppendLine("<td colspan='3'>-</td>");
                            result.AppendLine("</tr>");
                            continue;
                        }

                        result.AppendLine($"<td>{templatePath}</td>");

                        // Проверяем существование шаблона
                        if (fullTemplatePath == null || !System.IO.File.Exists(fullTemplatePath))
                        {
                            result.AppendLine("<td class='text-danger'>Шаблон не найден</td>");
                            result.AppendLine("<td colspan='3'>-</td>");
                            result.AppendLine("</tr>");
                            continue;
                        }

                        // Получаем поля из JSON
                        var fields = new List<string>();
                        if (root.TryGetProperty("fields", out var fieldsElement) && fieldsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var field in fieldsElement.EnumerateArray())
                            {
                                if (field.TryGetProperty("fieldName", out var fieldNameElement))
                                {
                                    fields.Add(fieldNameElement.GetString());
                                }
                            }
                        }

                        // Читаем шаблон и находим плейсхолдеры
                        var extension = Path.GetExtension(fullTemplatePath).ToLowerInvariant();
                        var placeholders = new List<string>();

                        if (extension == ".docx")
                        {
                            // Для DOCX используем библиотеку DocX
                            try
                            {
                                using (var doc = Xceed.Words.NET.DocX.Load(fullTemplatePath))
                                {
                                    var text = doc.Text;
                                    var regex = new Regex(@"\{\{([^}]+)\}\}");
                                    var matches = regex.Matches(text);

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
                                _logger.LogError(ex, $"Ошибка при чтении DOCX файла {fullTemplatePath}");
                                result.AppendLine($"<td class='text-danger'>Ошибка чтения DOCX: {ex.Message}</td>");
                                result.AppendLine("<td colspan='3'>-</td>");
                                result.AppendLine("</tr>");
                                continue;
                            }
                        }
                        else
                        {
                            // Для DOC используем текстовый подход
                            try
                            {
                                // Регистрируем поддержку кодовых страниц Windows
                                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                                // Пробуем разные кодировки
                                var encodings = new List<Encoding>
                                {
                                    Encoding.GetEncoding(1251),  // Windows-1251 для русского языка
                                    Encoding.UTF8,               // UTF-8 для универсальной поддержки
                                    Encoding.ASCII               // ASCII для базовых символов
                                };

                                string content = null;

                                foreach (var encoding in encodings)
                                {
                                    try
                                    {
                                        content = System.IO.File.ReadAllText(fullTemplatePath, encoding);

                                        // Ищем плейсхолдеры в тексте
                                        var regex = new Regex(@"\{\{([^}]+)\}\}");
                                        var matches = regex.Matches(content);

                                        if (matches.Count > 0)
                                        {
                                            foreach (Match match in matches)
                                            {
                                                if (match.Groups.Count > 1)
                                                {
                                                    placeholders.Add(match.Groups[1].Value);
                                                }
                                            }

                                            // Если нашли плейсхолдеры, останавливаемся
                                            break;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        // Игнорируем ошибки кодировки
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Ошибка при чтении DOC файла {fullTemplatePath}");
                                result.AppendLine($"<td class='text-danger'>Ошибка чтения DOC: {ex.Message}</td>");
                                result.AppendLine("<td colspan='3'>-</td>");
                                result.AppendLine("</tr>");
                                continue;
                            }
                        }

                        // Удаляем дубликаты и сортируем
                        placeholders = placeholders.Distinct().OrderBy(p => p).ToList();
                        fields = fields.OrderBy(f => f).ToList();

                        // Проверяем соответствие полей и плейсхолдеров
                        var missingFields = placeholders.Except(fields).ToList();

                        if (missingFields.Count > 0)
                        {
                            result.AppendLine("<td class='text-warning'>Несоответствие полей</td>");
                        }
                        else
                        {
                            result.AppendLine("<td class='text-success'>OK</td>");
                        }

                        // Отображаем поля и плейсхолдеры
                        result.AppendLine($"<td>{string.Join("<br>", fields)}</td>");
                        result.AppendLine($"<td>{string.Join("<br>", placeholders)}</td>");
                        result.AppendLine($"<td class='text-danger'>{string.Join("<br>", missingFields)}</td>");
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine("<td colspan='5' class='text-danger'>Ошибка обработки: " + ex.Message + "</td>");
                    }

                    result.AppendLine("</tr>");
                }

                result.AppendLine("</tbody></table>");

                result.AppendLine("<div class='mt-4'>");
                result.AppendLine("<a href='/' class='btn btn-secondary'>Вернуться на главную</a>");
                result.AppendLine("</div>");
            }
            catch (Exception ex)
            {
                result.AppendLine("<div class='alert alert-danger'>");
                result.AppendLine($"<p>Произошла ошибка: {ex.Message}</p>");
                result.AppendLine($"<pre>{ex.StackTrace}</pre>");
                result.AppendLine("</div>");
            }

            result.AppendLine("</div></body></html>");

            return Content(result.ToString(), "text/html");
        }

        [HttpGet("create-simple-templates")]
        public IActionResult CreateSimpleTemplates()
        {
            var result = new StringBuilder();
            result.AppendLine("<!DOCTYPE html>");
            result.AppendLine("<html><head>");
            result.AppendLine("<meta charset='utf-8'>");
            result.AppendLine("<title>Создание простых шаблонов</title>");
            result.AppendLine("<link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'>");
            result.AppendLine("</head><body>");
            result.AppendLine("<div class='container mt-4'>");
            result.AppendLine("<h1>Создание простых текстовых шаблонов</h1>");

            try
            {
                // Путь к директории с JSON-файлами
                var jsonDir = Path.Combine(_environment.WebRootPath, "Templates", "Json");
                var templatesDir = Path.Combine(_environment.WebRootPath, "Templates", "Word");

                if (!Directory.Exists(jsonDir))
                {
                    result.AppendLine("<div class='alert alert-danger'>Директория JSON не найдена</div>");
                    return Content(result.ToString(), "text/html");
                }

                // Создаем директорию для шаблонов, если нужно
                Directory.CreateDirectory(templatesDir);

                // Получаем все JSON-файлы для упаковочных листов
                var jsonFiles = Directory.GetFiles(jsonDir, "*.json", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileName(f).Contains("Уп. л.") || Path.GetFileName(f).StartsWith("ЭРЧМ30Т2.22.00."))
                    .ToList();

                result.AppendLine($"<div class='alert alert-info'>Найдено {jsonFiles.Count} файлов упаковочных листов</div>");
                result.AppendLine("<table class='table table-striped'>");
                result.AppendLine("<thead><tr>");
                result.AppendLine("<th>JSON файл</th>");
                result.AppendLine("<th>Путь к шаблону</th>");
                result.AppendLine("<th>Содержимое шаблона</th>");
                result.AppendLine("<th>Статус</th>");
                result.AppendLine("</tr></thead><tbody>");

                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        var relativePath = Path.GetRelativePath(jsonDir, jsonFile);
                        result.AppendLine("<tr>");
                        result.AppendLine($"<td>{relativePath}</td>");

                        // Читаем JSON-файл
                        var jsonContent = System.IO.File.ReadAllText(jsonFile);
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                        var root = jsonDoc.RootElement;

                        // Определяем путь для шаблона из JSON
                        string templatePath = null;
                        string documentName = null;

                        if (root.TryGetProperty("templatePath", out var templatePathElement))
                        {
                            templatePath = templatePathElement.GetString();
                        }

                        if (root.TryGetProperty("name", out var nameElement))
                        {
                            documentName = nameElement.GetString();
                        }

                        if (string.IsNullOrEmpty(templatePath))
                        {
                            result.AppendLine("<td colspan='3' class='text-danger'>Путь к шаблону не указан в JSON</td>");
                            result.AppendLine("</tr>");
                            continue;
                        }

                        result.AppendLine($"<td>{templatePath}</td>");

                        // Полный путь к шаблону
                        var fullTemplatePath = Path.Combine(_environment.WebRootPath, templatePath);

                        // Создаем директорию для шаблона, если нужно
                        Directory.CreateDirectory(Path.GetDirectoryName(fullTemplatePath));

                        // Получаем поля из JSON
                        var fields = new List<string>();

                        if (root.TryGetProperty("fields", out var fieldsElement) && fieldsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var field in fieldsElement.EnumerateArray())
                            {
                                if (field.TryGetProperty("fieldName", out var fieldNameElement))
                                {
                                    fields.Add(fieldNameElement.GetString());
                                }
                            }
                        }

                        // Для упаковочного листа №1
                        string templateContent = "";

                        if (templatePath.Contains("101-07"))
                        {
                            templateContent = $@"УПАКОВОЧНЫЙ ЛИСТ №1
Регулятор частоты вращения электродвигателей
ЭРЧМ30Т3-10
зав. № {{{{ FactoryNumber }}}}

Дата упаковки: {{{{ PackagingDate }}}}

Комплектность изделия:
1. Блок управления БУ 30Т3-10
   зав. № {{{{ ControllerSerialNumber }}}}
2. Устройство исполнительное
   ЭГУ 104П зав. № {{{{ ExecutiveDeviceSerialNumber }}}}
3. Преобразователь частоты вращения зав. № {{{{ FrequencyConverterSerialNumber_1 }}}}
   (длина кабеля L=2800±40мм)
4. Преобразователь частоты вращения зав. № {{{{ FrequencyConverterSerialNumber_2 }}}}
";
                        }
                        // Для упаковочного листа №2
                        else if (templatePath.Contains("201-07"))
                        {
                            templateContent = $@"УПАКОВОЧНЫЙ ЛИСТ №2
Регулятор частоты вращения электродвигателей
ЭРЧМ30Т3-10
зав. № {{{{ FactoryNumber }}}}

Дата упаковки: {{{{ PackagingDate }}}}

Комплектность изделия:
1. Блок питания БП 110/24-2-15/30
   № {{{{ PowerSupplySerialNumber }}}}
2. Преобразователь давления 
   (гидравлический 16 бар № {{{{ PressureConverterSerial_16 }}}} , длина кабеля L=2300±40мм)
3. Преобразователь давления  
   (гидравлический 2,5 бар № {{{{ PressureConverterSerial_2_5 }}}} , длина кабеля L=2900±40мм)
";
                        }
                        else
                        {
                            templateContent = $@"ШАБЛОН ДОКУМЕНТА: {documentName ?? Path.GetFileNameWithoutExtension(jsonFile)}
==============================================

Плейсхолдеры для замены:

";
                            foreach (var field in fields)
                            {
                                templateContent += $"{field}: {{{{ {field} }}}}\n";
                            }
                        }

                        result.AppendLine($"<td><pre>{templateContent}</pre></td>");

                        // Сохраняем шаблон в формате DOC (текстовый файл)
                        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                        System.IO.File.WriteAllText(fullTemplatePath, templateContent, Encoding.GetEncoding(1251));

                        result.AppendLine("<td class='text-success'>Создан успешно</td>");
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"<td colspan='2' class='text-danger'>Ошибка: {ex.Message}</td>");
                    }

                    result.AppendLine("</tr>");
                }

                result.AppendLine("</tbody></table>");

                result.AppendLine("<div class='mt-4'>");
                result.AppendLine("<a href='/template-tools/validate' class='btn btn-primary me-2'>Проверить шаблоны</a>");
                result.AppendLine("<a href='/' class='btn btn-secondary'>Вернуться на главную</a>");
                result.AppendLine("</div>");
            }
            catch (Exception ex)
            {
                result.AppendLine("<div class='alert alert-danger'>");
                result.AppendLine($"<p>Произошла ошибка: {ex.Message}</p>");
                result.AppendLine($"<pre>{ex.StackTrace}</pre>");
                result.AppendLine("</div>");
            }

            result.AppendLine("</div></body></html>");

            return Content(result.ToString(), "text/html");
        }
    }
}