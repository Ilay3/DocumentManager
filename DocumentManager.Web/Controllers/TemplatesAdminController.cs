// DocumentManager.Web/Controllers/TemplatesAdminController.cs
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Services;
using DocumentManager.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Index(string filter = null)
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
                    Filter = filter
                };

                // Применяем фильтр, если он задан
                if (!string.IsNullOrEmpty(filter))
                {
                    viewModel.Templates = viewModel.Templates
                        .Where(t =>
                            t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            t.Code.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка шаблонов");
                TempData["ErrorMessage"] = $"Ошибка при получении списка шаблонов: {ex.Message}";
                return View(new TemplatesAdminViewModel());
            }
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

        
    }
}