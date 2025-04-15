// DocumentManager.Web/Controllers/UtilityController.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentManager.Web.Controllers
{
    public class UtilityController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UtilityController> _logger;

        public UtilityController(
            IWebHostEnvironment environment,
            ILogger<UtilityController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        // GET: /Utility/CopyFiles
        public IActionResult CopyFiles()
        {
            var result = new StringBuilder();
            result.AppendLine("<h1>Утилита копирования файлов</h1>");

            try
            {
                // Пути источников и назначения
                var wwwrootPath = _environment.WebRootPath;
                var templatesDir = Path.Combine(_environment.ContentRootPath, "Templates");

                // Путь к исходным JSON файлам в wwwroot
                var sourceJsonDir = Path.Combine(wwwrootPath, "Templates", "Json");
                // Путь к исходным Word файлам в wwwroot
                var sourceWordDir = Path.Combine(wwwrootPath, "Templates", "Word");

                // Путь к целевым JSON файлам (относительно корня проекта)
                var targetJsonDir = Path.Combine(templatesDir, "Json");
                // Путь к целевым Word файлам (относительно корня проекта)
                var targetWordDir = Path.Combine(templatesDir, "Word");

                // Создаем директории, если они не существуют
                Directory.CreateDirectory(targetJsonDir);
                Directory.CreateDirectory(targetWordDir);

                result.AppendLine("<h2>Структура исходных директорий:</h2>");
                result.AppendLine("<pre>");
                result.AppendLine($"WebRootPath: {wwwrootPath}");
                result.AppendLine($"JSON Source: {sourceJsonDir}");
                result.AppendLine($"Word Source: {sourceWordDir}");
                result.AppendLine($"JSON Target: {targetJsonDir}");
                result.AppendLine($"Word Target: {targetWordDir}");
                result.AppendLine("</pre>");

                // Копируем JSON файлы
                result.AppendLine("<h2>Копирование JSON файлов:</h2>");
                result.AppendLine("<ul>");

                if (Directory.Exists(sourceJsonDir))
                {
                    foreach (var sourceDir in Directory.GetDirectories(sourceJsonDir))
                    {
                        var dirName = Path.GetFileName(sourceDir);
                        var targetSubDir = Path.Combine(targetJsonDir, dirName);
                        Directory.CreateDirectory(targetSubDir);

                        foreach (var file in Directory.GetFiles(sourceDir, "*.json"))
                        {
                            var fileName = Path.GetFileName(file);
                            var targetFile = Path.Combine(targetSubDir, fileName);
                            System.IO.File.Copy(file, targetFile, true);
                            result.AppendLine($"<li>Скопирован: {Path.Combine(dirName, fileName)}</li>");
                        }
                    }
                }
                else
                {
                    result.AppendLine($"<li class='text-danger'>Исходная директория JSON не найдена: {sourceJsonDir}</li>");
                }

                result.AppendLine("</ul>");

                // Копируем Word файлы
                result.AppendLine("<h2>Копирование Word файлов:</h2>");
                result.AppendLine("<ul>");

                if (Directory.Exists(sourceWordDir))
                {
                    foreach (var sourceDir in Directory.GetDirectories(sourceWordDir))
                    {
                        var dirName = Path.GetFileName(sourceDir);
                        var targetSubDir = Path.Combine(targetWordDir, dirName);
                        Directory.CreateDirectory(targetSubDir);

                        foreach (var file in Directory.GetFiles(sourceDir, "*.doc*"))
                        {
                            var fileName = Path.GetFileName(file);
                            var targetFile = Path.Combine(targetSubDir, fileName);
                            System.IO.File.Copy(file, targetFile, true);
                            result.AppendLine($"<li>Скопирован: {Path.Combine(dirName, fileName)}</li>");
                        }
                    }
                }
                else
                {
                    result.AppendLine($"<li class='text-danger'>Исходная директория Word не найдена: {sourceWordDir}</li>");
                }

                result.AppendLine("</ul>");

                // Сканируем целевые директории
                result.AppendLine("<h2>Сканирование целевых директорий:</h2>");

                result.AppendLine("<h3>JSON файлы:</h3>");
                result.AppendLine("<ul>");
                if (Directory.Exists(targetJsonDir))
                {
                    var jsonFiles = Directory.GetFiles(targetJsonDir, "*.json", SearchOption.AllDirectories);
                    foreach (var file in jsonFiles)
                    {
                        result.AppendLine($"<li>{Path.GetRelativePath(targetJsonDir, file)}</li>");
                    }

                    if (jsonFiles.Length == 0)
                    {
                        result.AppendLine("<li class='text-warning'>Файлы не найдены</li>");
                    }
                }
                else
                {
                    result.AppendLine("<li class='text-danger'>Директория не существует</li>");
                }
                result.AppendLine("</ul>");

                result.AppendLine("<h3>Word файлы:</h3>");
                result.AppendLine("<ul>");
                if (Directory.Exists(targetWordDir))
                {
                    var wordFiles = Directory.GetFiles(targetWordDir, "*.doc*", SearchOption.AllDirectories);
                    foreach (var file in wordFiles)
                    {
                        result.AppendLine($"<li>{Path.GetRelativePath(targetWordDir, file)}</li>");
                    }

                    if (wordFiles.Length == 0)
                    {
                        result.AppendLine("<li class='text-warning'>Файлы не найдены</li>");
                    }
                }
                else
                {
                    result.AppendLine("<li class='text-danger'>Директория не существует</li>");
                }
                result.AppendLine("</ul>");

                // Добавляем ссылку на инициализацию
                result.AppendLine("<hr>");
                result.AppendLine("<div>");
                result.AppendLine("<a href='/Admin/Initialize' class='btn btn-primary'>Запустить инициализацию шаблонов</a> ");
                result.AppendLine("<a href='/Diagnostics' class='btn btn-info'>Проверить диагностику</a>");
                result.AppendLine("</div>");
            }
            catch (Exception ex)
            {
                result.AppendLine("<h2 class='text-danger'>Ошибка:</h2>");
                result.AppendLine("<pre>");
                result.AppendLine(ex.ToString());
                result.AppendLine("</pre>");
            }

            return Content(result.ToString(), "text/html");
        }

        // GET: /Utility/CheckJsonFiles
        public IActionResult CheckJsonFiles()
        {
            var result = new StringBuilder();
            result.AppendLine("<h1>Проверка JSON-файлов</h1>");

            try
            {
                var wwwrootPath = _environment.WebRootPath;
                var jsonPath = Path.Combine(wwwrootPath, "Templates", "Json");

                result.AppendLine($"<h2>Путь к JSON-файлам: {jsonPath}</h2>");

                if (!Directory.Exists(jsonPath))
                {
                    result.AppendLine("<p class='text-danger'>Директория не существует!</p>");
                    return Content(result.ToString(), "text/html");
                }

                // Получаем все JSON-файлы
                var jsonFiles = Directory.GetFiles(jsonPath, "*.json", SearchOption.AllDirectories);

                if (jsonFiles.Length == 0)
                {
                    result.AppendLine("<p class='text-warning'>JSON-файлы не найдены!</p>");
                    return Content(result.ToString(), "text/html");
                }

                result.AppendLine($"<p>Найдено файлов: {jsonFiles.Length}</p>");

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        result.AppendLine("<div style='margin-bottom: 20px; border: 1px solid #ddd; padding: 10px;'>");
                        result.AppendLine($"<h3>Файл: {Path.GetRelativePath(jsonPath, file)}</h3>");

                        // Читаем содержимое файла
                        var fileContent = System.IO.File.ReadAllText(file);

                        // Пытаемся десериализовать JSON
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(fileContent);
                        var root = jsonDoc.RootElement;

                        // Проверяем необходимые поля
                        bool hasId = root.TryGetProperty("id", out var idElement);
                        bool hasName = root.TryGetProperty("name", out var nameElement);
                        bool hasTemplatePath = root.TryGetProperty("templatePath", out var templatePathElement);
                        bool hasFields = root.TryGetProperty("fields", out var fieldsElement);

                        // Выводим результаты проверки
                        result.AppendLine("<table border='1' cellpadding='5' style='border-collapse: collapse;'>");
                        result.AppendLine("<tr><th>Поле</th><th>Наличие</th><th>Значение</th></tr>");

                        result.AppendLine($"<tr><td>id</td><td>{(hasId ? "✓" : "✗")}</td><td>{(hasId ? idElement.GetString() : "")}</td></tr>");
                        result.AppendLine($"<tr><td>name</td><td>{(hasName ? "✓" : "✗")}</td><td>{(hasName ? nameElement.GetString() : "")}</td></tr>");
                        result.AppendLine($"<tr><td>templatePath</td><td>{(hasTemplatePath ? "✓" : "✗")}</td><td>{(hasTemplatePath ? templatePathElement.GetString() : "")}</td></tr>");
                        result.AppendLine($"<tr><td>fields</td><td>{(hasFields ? "✓" : "✗")}</td><td>{(hasFields ? $"Полей: {fieldsElement.GetArrayLength()}" : "")}</td></tr>");

                        result.AppendLine("</table>");

                        // Если есть поле templatePath, проверяем наличие соответствующего Word-файла
                        if (hasTemplatePath)
                        {
                            var templatePath = templatePathElement.GetString();
                            var wordPath = Path.Combine(wwwrootPath, "Templates", "Word", templatePath);

                            bool wordExists = System.IO.File.Exists(wordPath);

                            result.AppendLine("<p>Проверка Word-файла:</p>");
                            result.AppendLine($"<p>Путь: {wordPath}</p>");
                            result.AppendLine($"<p>Существует: {(wordExists ? "✓" : "✗")}</p>");

                            if (!wordExists)
                            {
                                // Пытаемся найти файл по имени
                                var fileBaseName = Path.GetFileNameWithoutExtension(templatePath);
                                var wordDir = Path.Combine(wwwrootPath, "Templates", "Word");

                                var alternatives = Directory.GetFiles(wordDir, $"{fileBaseName}*.doc*", SearchOption.AllDirectories);

                                if (alternatives.Length > 0)
                                {
                                    result.AppendLine("<p>Найдены альтернативные файлы:</p>");
                                    result.AppendLine("<ul>");

                                    foreach (var alt in alternatives)
                                    {
                                        result.AppendLine($"<li>{Path.GetRelativePath(wordDir, alt)}</li>");
                                    }

                                    result.AppendLine("</ul>");
                                }
                            }
                        }

                        // Показываем содержимое файла
                        result.AppendLine("<p>Содержимое файла:</p>");
                        result.AppendLine($"<pre style='background-color: #f5f5f5; padding: 10px; overflow: auto; max-height: 300px;'>{fileContent}</pre>");

                        result.AppendLine("</div>");
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"<div class='text-danger'>Ошибка при обработке файла {Path.GetFileName(file)}: {ex.Message}</div>");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AppendLine("<h2 class='text-danger'>Ошибка:</h2>");
                result.AppendLine("<pre>");
                result.AppendLine(ex.ToString());
                result.AppendLine("</pre>");
            }

            return Content(result.ToString(), "text/html");
        }

        // GET: /Utility/FixJsonPaths
        // GET: /Utility/FixJsonPaths
        // GET: /Utility/FixJsonPaths
        // GET: /Utility/FixJsonPaths
        public IActionResult FixJsonPaths()
        {
            // Зарегистрировать поддержку кодовых страниц (проверьте, что пакет System.Text.Encoding.CodePages подключён)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            // Получаем объект кодировки Windows-1251
            var encoding1251 = System.Text.Encoding.GetEncoding("windows-1251");

            var result = new StringBuilder();
            result.AppendLine("<!DOCTYPE html>");
            result.AppendLine("<html><head><meta charset=\"utf-8\"><title>Исправление путей в JSON-файлах</title></head><body>");
            result.AppendLine("<h1>Исправление путей в JSON-файлах</h1>");

            try
            {
                // Определяем пути
                var wwwrootPath = _environment.WebRootPath;
                var jsonPath = Path.Combine(wwwrootPath, "Templates", "Json");

                result.AppendLine($"<h2>Путь к JSON-файлам: {jsonPath}</h2>");

                if (!Directory.Exists(jsonPath))
                {
                    result.AppendLine("<p class='text-danger'>Директория не существует!</p>");
                    result.AppendLine("</body></html>");
                    return Content(result.ToString(), "text/html; charset=utf-8");
                }

                // Получаем все JSON-файлы
                var jsonFiles = Directory.GetFiles(jsonPath, "*.json", SearchOption.AllDirectories);

                if (jsonFiles.Length == 0)
                {
                    result.AppendLine("<p class='text-warning'>JSON-файлы не найдены!</p>");
                    result.AppendLine("</body></html>");
                    return Content(result.ToString(), "text/html; charset=utf-8");
                }

                result.AppendLine($"<p>Найдено файлов: {jsonFiles.Length}</p>");
                result.AppendLine("<ul>");

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var currentFileName = Path.GetFileName(file);
                        // Читаем содержимое файла с кодировкой Windows-1251
                        var fileContent = System.IO.File.ReadAllText(file, encoding1251);

                        // Пытаемся десериализовать JSON для проверки поля templatePath
                        using var jsonDoc = System.Text.Json.JsonDocument.Parse(fileContent);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("templatePath", out var templatePathElement))
                        {
                            var templatePath = templatePathElement.GetString();
                            var fileDir = Path.GetDirectoryName(file);
                            var relativeDir = Path.GetRelativePath(jsonPath, fileDir);
                            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(currentFileName);
                            var wordDir = Path.Combine(wwwrootPath, "Templates", "Word", relativeDir);

                            if (Directory.Exists(wordDir))
                            {
                                var wordFiles = Directory.GetFiles(wordDir, $"{fileNameWithoutExt}*.doc*");
                                if (wordFiles.Length > 0)
                                {
                                    var wordFile = wordFiles[0];
                                    var newTemplatePath = Path.Combine("Templates", relativeDir, Path.GetFileName(wordFile));
                                    newTemplatePath = newTemplatePath.Replace('\\', '/');

                                    var jsonObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(fileContent);
                                    jsonObj["templatePath"] = newTemplatePath;

                                    var newJsonContent = System.Text.Json.JsonSerializer.Serialize(jsonObj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                                    // Сохраняем JSON в UTF-8
                                    System.IO.File.WriteAllText(file, newJsonContent, System.Text.Encoding.UTF8);

                                    result.AppendLine($"<li>Файл {currentFileName}: Путь изменён с '{templatePath}' на '{newTemplatePath}'</li>");
                                }
                                else
                                {
                                    result.AppendLine($"<li class='text-warning'>Файл {currentFileName}: Word-файл не найден в {wordDir}</li>");
                                }
                            }
                            else
                            {
                                result.AppendLine($"<li class='text-warning'>Файл {currentFileName}: Директория {wordDir} не существует</li>");
                            }
                        }
                        else
                        {
                            result.AppendLine($"<li class='text-warning'>Файл {currentFileName}: Поле templatePath не найдено</li>");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"<li class='text-danger'>Ошибка при обработке файла {Path.GetFileName(file)}: {ex.Message}</li>");
                    }
                }

                result.AppendLine("</ul>");
                result.AppendLine("<hr>");
                result.AppendLine("<div>");
                result.AppendLine("<a href='/Utility/CheckJsonFiles' class='btn btn-info'>Проверить JSON-файлы</a> ");
                result.AppendLine("<a href='/Admin/Initialize' class='btn btn-primary'>Запустить инициализацию шаблонов</a> ");
                result.AppendLine("<a href='/Diagnostics' class='btn btn-info'>Проверить диагностику</a>");
                result.AppendLine("</div>");
            }
            catch (Exception ex)
            {
                result.AppendLine("<h2 class='text-danger'>Ошибка:</h2>");
                result.AppendLine("<pre>");
                result.AppendLine(ex.ToString());
                result.AppendLine("</pre>");
            }
            result.AppendLine("</body></html>");

            return Content(result.ToString(), "text/html; charset=utf-8");
        }

    }
}