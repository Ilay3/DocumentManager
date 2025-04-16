using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DocumentManager.Web.Controllers
{
    [Route("path-fixer")]
    public class PathFixerController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PathFixerController> _logger;

        public PathFixerController(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<PathFixerController> logger)
        {
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("fix-all-paths")]
        public IActionResult FixAllPaths()
        {
            var result = new StringBuilder();
            result.AppendLine("<!DOCTYPE html>");
            result.AppendLine("<html><head>");
            result.AppendLine("<meta charset='utf-8'>");
            result.AppendLine("<title>Исправление путей в JSON</title>");
            result.AppendLine("<link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'>");
            result.AppendLine("</head><body>");
            result.AppendLine("<div class='container mt-4'>");
            result.AppendLine("<h1>Автоматическое исправление путей к шаблонам Word</h1>");

            try
            {
                // Получаем пути к директориям
                var templatesBasePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("TemplatesBasePath") ?? "Templates/Word"
                );

                var jsonBasePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("JsonBasePath") ?? "Templates/Json"
                );

                // Проверяем существование директорий
                if (!Directory.Exists(templatesBasePath))
                {
                    result.AppendLine("<div class='alert alert-danger'>Директория шаблонов Word не найдена</div>");
                    return Content(result.ToString(), "text/html");
                }

                if (!Directory.Exists(jsonBasePath))
                {
                    result.AppendLine("<div class='alert alert-danger'>Директория JSON-файлов не найдена</div>");
                    return Content(result.ToString(), "text/html");
                }

                // Получаем все Word-файлы
                var wordFiles = Directory.GetFiles(templatesBasePath, "*.doc*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .ToList();

                // Получаем все JSON-файлы
                var jsonFiles = Directory.GetFiles(jsonBasePath, "*.json", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .ToList();

                result.AppendLine("<div class='alert alert-info'>");
                result.AppendLine($"Найдено {wordFiles.Count} файлов Word и {jsonFiles.Count} JSON-файлов");
                result.AppendLine("</div>");

                // Таблица результатов
                result.AppendLine("<table class='table table-striped table-bordered'>");
                result.AppendLine("<thead><tr>");
                result.AppendLine("<th>JSON файл</th>");
                result.AppendLine("<th>Старый путь</th>");
                result.AppendLine("<th>Новый путь</th>");
                result.AppendLine("<th>Статус</th>");
                result.AppendLine("</tr></thead><tbody>");

                int fixedCount = 0;
                List<string> errors = new List<string>();

                // Обрабатываем каждый JSON файл
                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        string jsonRelativePath = Path.GetRelativePath(jsonBasePath, jsonFile.FullName);
                        string jsonContent = System.IO.File.ReadAllText(jsonFile.FullName);
                        JsonNode jsonNode = JsonNode.Parse(jsonContent);

                        // Получаем текущий путь к шаблону
                        string oldTemplatePath = null;
                        if (jsonNode["templatePath"] != null)
                        {
                            oldTemplatePath = jsonNode["templatePath"].GetValue<string>();
                        }

                        // Получаем имя шаблона из JSON
                        string documentCode = null;
                        if (jsonNode["id"] != null)
                        {
                            documentCode = jsonNode["id"].GetValue<string>();
                        }

                        string documentName = null;
                        if (jsonNode["name"] != null)
                        {
                            documentName = jsonNode["name"].GetValue<string>();
                        }

                        // Пытаемся найти подходящий Word файл
                        FileInfo matchingWordFile = null;
                        string newTemplatePath = null;

                        // Сначала пробуем найти по старому пути, если он указан
                        if (!string.IsNullOrEmpty(oldTemplatePath))
                        {
                            string oldFullPath = Path.Combine(_environment.WebRootPath, oldTemplatePath);
                            if (System.IO.File.Exists(oldFullPath))
                            {
                                matchingWordFile = new FileInfo(oldFullPath);
                            }
                            else
                            {
                                // Ищем файл только по имени файла, игнорируя путь
                                string oldFileName = Path.GetFileName(oldTemplatePath);
                                matchingWordFile = wordFiles.FirstOrDefault(f =>
                                    f.Name.Equals(oldFileName, StringComparison.OrdinalIgnoreCase));
                            }
                        }

                        // Если не нашли по пути, пробуем искать по имени/коду документа
                        if (matchingWordFile == null && !string.IsNullOrEmpty(documentCode))
                        {
                            matchingWordFile = wordFiles.FirstOrDefault(f =>
                                f.Name.Contains(documentCode, StringComparison.OrdinalIgnoreCase));
                        }

                        // Если все еще не нашли, ищем по имени документа
                        if (matchingWordFile == null && !string.IsNullOrEmpty(documentName))
                        {
                            // Извлекаем код из имени документа
                            int indexOfPS = documentName.IndexOf(" ПС ");
                            if (indexOfPS > 0)
                            {
                                string codeFromName = documentName.Substring(0, indexOfPS).Trim();
                                matchingWordFile = wordFiles.FirstOrDefault(f =>
                                    f.Name.Contains(codeFromName, StringComparison.OrdinalIgnoreCase));
                            }
                            else
                            {
                                // Проверяем, содержит ли имя файла Word часть имени из JSON
                                foreach (var wordFile in wordFiles)
                                {
                                    if (wordFile.Name.Contains(Path.GetFileNameWithoutExtension(jsonFile.Name), StringComparison.OrdinalIgnoreCase) ||
                                        Path.GetFileNameWithoutExtension(jsonFile.Name).Contains(Path.GetFileNameWithoutExtension(wordFile.Name), StringComparison.OrdinalIgnoreCase))
                                    {
                                        matchingWordFile = wordFile;
                                        break;
                                    }
                                }
                            }
                        }

                        // Если по-прежнему не нашли, но старый путь указывает на определенную подпапку, ищем там
                        if (matchingWordFile == null && !string.IsNullOrEmpty(oldTemplatePath))
                        {
                            string oldDir = Path.GetDirectoryName(oldTemplatePath).Replace('\\', '/');
                            if (oldDir.Contains("/"))
                            {
                                string subFolder = oldDir.Split('/').Last();
                                var filesInSubfolder = wordFiles.Where(f =>
                                    f.Directory.Name.Equals(subFolder, StringComparison.OrdinalIgnoreCase)).ToList();

                                if (filesInSubfolder.Count == 1)
                                {
                                    // Если в папке только один файл, берем его
                                    matchingWordFile = filesInSubfolder.First();
                                }
                                else if (filesInSubfolder.Count > 1 && !string.IsNullOrEmpty(documentCode))
                                {
                                    // Если в папке несколько файлов, ищем по коду
                                    matchingWordFile = filesInSubfolder.FirstOrDefault(f =>
                                        f.Name.Contains(documentCode, StringComparison.OrdinalIgnoreCase));
                                }
                            }
                        }

                        // Если нашли подходящий файл
                        if (matchingWordFile != null)
                        {
                            // Формируем новый путь относительно веб-корня
                            string wordRelativePath = Path.GetRelativePath(_environment.WebRootPath, matchingWordFile.FullName);
                            newTemplatePath = wordRelativePath.Replace("\\", "/");

                            // Обновляем JSON
                            jsonNode["templatePath"] = newTemplatePath;
                            string updatedJsonContent = jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

                            // Создаем бэкап
                            string backupPath = jsonFile.FullName + ".bak";
                            System.IO.File.Copy(jsonFile.FullName, backupPath, true);

                            // Записываем обновленный JSON
                            System.IO.File.WriteAllText(jsonFile.FullName, updatedJsonContent);

                            fixedCount++;

                            result.AppendLine("<tr class='table-success'>");
                            result.AppendLine($"<td>{jsonRelativePath}</td>");
                            result.AppendLine($"<td>{oldTemplatePath}</td>");
                            result.AppendLine($"<td>{newTemplatePath}</td>");
                            result.AppendLine("<td>Исправлено</td>");
                            result.AppendLine("</tr>");
                        }
                        else
                        {
                            // Не нашли подходящий файл
                            errors.Add($"Не удалось найти подходящий Word файл для {jsonRelativePath}");

                            result.AppendLine("<tr class='table-danger'>");
                            result.AppendLine($"<td>{jsonRelativePath}</td>");
                            result.AppendLine($"<td>{oldTemplatePath}</td>");
                            result.AppendLine("<td>-</td>");
                            result.AppendLine("<td>Ошибка: файл не найден</td>");
                            result.AppendLine("</tr>");
                        }
                    }
                    catch (Exception ex)
                    {
                        string jsonRelativePath = Path.GetRelativePath(jsonBasePath, jsonFile.FullName);
                        errors.Add($"Ошибка при обработке {jsonRelativePath}: {ex.Message}");

                        result.AppendLine("<tr class='table-danger'>");
                        result.AppendLine($"<td>{jsonRelativePath}</td>");
                        result.AppendLine("<td>-</td>");
                        result.AppendLine("<td>-</td>");
                        result.AppendLine($"<td>Ошибка: {ex.Message}</td>");
                        result.AppendLine("</tr>");
                    }
                }

                result.AppendLine("</tbody></table>");

                // Итоги
                result.AppendLine("<div class='alert alert-info mt-4'>");
                result.AppendLine($"<p>Всего исправлено файлов: {fixedCount} из {jsonFiles.Count}</p>");
                if (errors.Count > 0)
                {
                    result.AppendLine("<p>Ошибки:</p>");
                    result.AppendLine("<ul>");
                    foreach (var error in errors)
                    {
                        result.AppendLine($"<li>{error}</li>");
                    }
                    result.AppendLine("</ul>");
                }
                result.AppendLine("</div>");

                // Предложение для ручного исправления
                if (jsonFiles.Count > fixedCount)
                {
                    result.AppendLine("<div class='alert alert-warning mt-4'>");
                    result.AppendLine("<p>Не все файлы удалось исправить автоматически. Вы можете воспользоваться утилитой ручного исправления:</p>");
                    result.AppendLine("<a href='/template-utility/analyze-files' class='btn btn-primary'>Перейти к ручному исправлению</a>");
                    result.AppendLine("</div>");
                }

                // Опция создания шаблонов
                result.AppendLine("<div class='card mt-4'>");
                result.AppendLine("<div class='card-header'>Создание отсутствующих шаблонов Word</div>");
                result.AppendLine("<div class='card-body'>");
                result.AppendLine("<p>Если некоторых шаблонов Word нет, вы можете создать их автоматически:</p>");
                result.AppendLine("<a href='/path-fixer/create-missing-templates' class='btn btn-success'>Создать отсутствующие шаблоны</a>");
                result.AppendLine("</div>");
                result.AppendLine("</div>");

                // Кнопки навигации
                result.AppendLine("<div class='mt-3'>");
                result.AppendLine("<a href='/template-utility/analyze-files' class='btn btn-secondary'>Вернуться к анализу файлов</a>");
                result.AppendLine("</div>");

            }
            catch (Exception ex)
            {
                result.AppendLine("<div class='alert alert-danger mt-4'>");
                result.AppendLine($"<p>Произошла ошибка: {ex.Message}</p>");
                result.AppendLine($"<pre>{ex.StackTrace}</pre>");
                result.AppendLine("</div>");
            }

            result.AppendLine("</div>"); // container
            result.AppendLine("</body></html>");

            return Content(result.ToString(), "text/html");
        }

        [HttpGet("create-missing-templates")]
        public IActionResult CreateMissingTemplates()
        {
            var result = new StringBuilder();
            result.AppendLine("<!DOCTYPE html>");
            result.AppendLine("<html><head>");
            result.AppendLine("<meta charset='utf-8'>");
            result.AppendLine("<title>Создание шаблонов Word</title>");
            result.AppendLine("<link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'>");
            result.AppendLine("</head><body>");
            result.AppendLine("<div class='container mt-4'>");
            result.AppendLine("<h1>Создание отсутствующих шаблонов Word</h1>");

            try
            {
                // Получаем пути к директориям
                var templatesBasePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("TemplatesBasePath") ?? "Templates/Word"
                );

                var jsonBasePath = Path.Combine(
                    _environment.WebRootPath,
                    _configuration.GetValue<string>("JsonBasePath") ?? "Templates/Json"
                );

                // Проверяем существование директорий
                if (!Directory.Exists(templatesBasePath))
                {
                    Directory.CreateDirectory(templatesBasePath);
                    result.AppendLine("<div class='alert alert-warning'>Директория шаблонов Word создана</div>");
                }

                if (!Directory.Exists(jsonBasePath))
                {
                    result.AppendLine("<div class='alert alert-danger'>Директория JSON-файлов не найдена</div>");
                    return Content(result.ToString(), "text/html");
                }

                // Получаем существующие Word-файлы
                var wordFiles = Directory.GetFiles(templatesBasePath, "*.doc*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .ToList();

                // Получаем все JSON-файлы
                var jsonFiles = Directory.GetFiles(jsonBasePath, "*.json", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .ToList();

                result.AppendLine("<div class='alert alert-info'>");
                result.AppendLine($"Найдено {wordFiles.Count} существующих файлов Word и {jsonFiles.Count} JSON-файлов");
                result.AppendLine("</div>");

                // Таблица результатов
                result.AppendLine("<table class='table table-striped table-bordered'>");
                result.AppendLine("<thead><tr>");
                result.AppendLine("<th>JSON файл</th>");
                result.AppendLine("<th>Путь к шаблону</th>");
                result.AppendLine("<th>Существует?</th>");
                result.AppendLine("<th>Действие</th>");
                result.AppendLine("</tr></thead><tbody>");

                int createdCount = 0;
                List<string> errors = new List<string>();

                // Обрабатываем каждый JSON файл
                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        string jsonRelativePath = Path.GetRelativePath(jsonBasePath, jsonFile.FullName);
                        string jsonContent = System.IO.File.ReadAllText(jsonFile.FullName);
                        JsonNode jsonNode = JsonNode.Parse(jsonContent);

                        // Получаем путь к шаблону из JSON
                        string templatePath = null;
                        if (jsonNode["templatePath"] != null)
                        {
                            templatePath = jsonNode["templatePath"].GetValue<string>();
                        }

                        if (string.IsNullOrEmpty(templatePath))
                        {
                            result.AppendLine("<tr class='table-warning'>");
                            result.AppendLine($"<td>{jsonRelativePath}</td>");
                            result.AppendLine("<td>Не указан</td>");
                            result.AppendLine("<td>-</td>");
                            result.AppendLine("<td>Пропущено: путь не указан</td>");
                            result.AppendLine("</tr>");
                            continue;
                        }

                        // Проверяем существование файла шаблона
                        string fullTemplatePath = Path.Combine(_environment.WebRootPath, templatePath);
                        bool templateExists = System.IO.File.Exists(fullTemplatePath);

                        if (templateExists)
                        {
                            result.AppendLine("<tr class='table-success'>");
                            result.AppendLine($"<td>{jsonRelativePath}</td>");
                            result.AppendLine($"<td>{templatePath}</td>");
                            result.AppendLine("<td>Да</td>");
                            result.AppendLine("<td>Пропущено: шаблон уже существует</td>");
                            result.AppendLine("</tr>");
                            continue;
                        }

                        // Создаем директорию для шаблона, если не существует
                        string templateDir = Path.GetDirectoryName(fullTemplatePath);
                        if (!Directory.Exists(templateDir))
                        {
                            Directory.CreateDirectory(templateDir);
                        }

                        // Получаем поля из JSON
                        List<string> fields = new List<string>();
                        if (jsonNode["fields"] is JsonArray fieldsArray)
                        {
                            foreach (var field in fieldsArray)
                            {
                                if (field["fieldName"] != null)
                                {
                                    fields.Add(field["fieldName"].GetValue<string>());
                                }
                            }
                        }

                        // Создаем шаблонный файл Word
                        CreateTemplateFile(fullTemplatePath, fields, Path.GetFileNameWithoutExtension(jsonFile.Name));
                        createdCount++;

                        result.AppendLine("<tr class='table-info'>");
                        result.AppendLine($"<td>{jsonRelativePath}</td>");
                        result.AppendLine($"<td>{templatePath}</td>");
                        result.AppendLine("<td>Создан</td>");
                        result.AppendLine($"<td>Создан шаблон с {fields.Count} полями</td>");
                        result.AppendLine("</tr>");
                    }
                    catch (Exception ex)
                    {
                        string jsonRelativePath = Path.GetRelativePath(jsonBasePath, jsonFile.FullName);
                        errors.Add($"Ошибка при обработке {jsonRelativePath}: {ex.Message}");

                        result.AppendLine("<tr class='table-danger'>");
                        result.AppendLine($"<td>{jsonRelativePath}</td>");
                        result.AppendLine("<td>-</td>");
                        result.AppendLine("<td>Ошибка</td>");
                        result.AppendLine($"<td>Ошибка: {ex.Message}</td>");
                        result.AppendLine("</tr>");
                    }
                }

                result.AppendLine("</tbody></table>");

                // Итоги
                result.AppendLine("<div class='alert alert-info mt-4'>");
                result.AppendLine($"<p>Всего создано шаблонов: {createdCount}</p>");
                if (errors.Count > 0)
                {
                    result.AppendLine("<p>Ошибки:</p>");
                    result.AppendLine("<ul>");
                    foreach (var error in errors)
                    {
                        result.AppendLine($"<li>{error}</li>");
                    }
                    result.AppendLine("</ul>");
                }
                result.AppendLine("</div>");

                // Кнопки навигации
                result.AppendLine("<div class='mt-3'>");
                result.AppendLine("<a href='/path-fixer/fix-all-paths' class='btn btn-primary me-2'>Исправить пути в JSON</a>");
                result.AppendLine("<a href='/template-utility/analyze-files' class='btn btn-secondary'>Вернуться к анализу файлов</a>");
                result.AppendLine("</div>");

            }
            catch (Exception ex)
            {
                result.AppendLine("<div class='alert alert-danger mt-4'>");
                result.AppendLine($"<p>Произошла ошибка: {ex.Message}</p>");
                result.AppendLine($"<pre>{ex.StackTrace}</pre>");
                result.AppendLine("</div>");
            }

            result.AppendLine("</div>"); // container
            result.AppendLine("</body></html>");

            return Content(result.ToString(), "text/html");
        }

        private void CreateTemplateFile(string filePath, List<string> fields, string title)
        {
            // Определяем тип файла
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".doc")
            {
                // Для .doc используем простой текстовый файл с указанием плейсхолдеров
                var content = new StringBuilder();
                content.AppendLine($"ШАБЛОН ДОКУМЕНТА: {title}");
                content.AppendLine("==============================================");
                content.AppendLine();
                content.AppendLine("Плейсхолдеры для замены:");
                content.AppendLine();

                foreach (var field in fields)
                {
                    content.AppendLine($"{field}: {{{{ {field} }}}}");
                }

                // Сохраняем в Windows-1251 для русских символов
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                System.IO.File.WriteAllText(filePath, content.ToString(), Encoding.GetEncoding(1251));
            }
            else
            {
                // Для .docx используем библиотеку DocX
                try
                {
                    var doc = Xceed.Words.NET.DocX.Create(filePath);

                    // Добавляем заголовок
                    var title1 = doc.InsertParagraph($"ШАБЛОН ДОКУМЕНТА: {title}");
                    title1.FontSize(16);
                    title1.Bold();

                    doc.InsertParagraph("==============================================");
                    doc.InsertParagraph();

                    var subtitle = doc.InsertParagraph("Плейсхолдеры для замены:");
                    subtitle.FontSize(14);
                    subtitle.Bold();

                    doc.InsertParagraph();

                    // Добавляем плейсхолдеры
                    foreach (var field in fields)
                    {
                        doc.InsertParagraph($"{field}: {{{{ {field} }}}}");
                    }

                    doc.Save();
                }
                catch
                {
                    // Если не удалось создать через DocX, создаем обычный текстовый файл
                    var content = new StringBuilder();
                    content.AppendLine($"ШАБЛОН ДОКУМЕНТА: {title}");
                    content.AppendLine("==============================================");
                    content.AppendLine();
                    content.AppendLine("Плейсхолдеры для замены:");
                    content.AppendLine();

                    foreach (var field in fields)
                    {
                        content.AppendLine($"{field}: {{{{ {field} }}}}");
                    }

                    System.IO.File.WriteAllText(filePath, content.ToString(), Encoding.UTF8);
                }
            }
        }
    }
}