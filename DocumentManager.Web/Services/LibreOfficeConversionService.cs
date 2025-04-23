using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DocumentManager.Web.Services
{
    /// <summary>
    /// Сервис для конвертации документов в PDF с использованием LibreOffice
    /// </summary>
    public class LibreOfficeConversionService
    {
        private readonly ILogger<LibreOfficeConversionService> _logger;
        private readonly string _libreOfficePath;
        private readonly string _tempFolder;
        private readonly string _outputBasePath;

        public LibreOfficeConversionService(
            ILogger<LibreOfficeConversionService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _libreOfficePath = configuration["LibreOffice:Path"] ?? @"C:\Program Files\LibreOffice\program\soffice.exe";
            _tempFolder = configuration["LibreOffice:TempFolder"] ?? Path.GetTempPath();
            _outputBasePath = configuration["OutputBasePath"] ?? Path.GetTempPath();

            // Проверка наличия LibreOffice
            if (!System.IO.File.Exists(_libreOfficePath))
            {
                _logger.LogWarning($"LibreOffice не найден по пути {_libreOfficePath}. Убедитесь, что путь указан верно в настройках.");
            }

            // Создание необходимых директорий
            Directory.CreateDirectory(_tempFolder);
            Directory.CreateDirectory(_outputBasePath);
            Directory.CreateDirectory(Path.Combine(_outputBasePath, "PDF"));
        }

        /// <summary>
        /// Конвертирует документ в PDF
        /// </summary>
        /// <param name="sourceFilePath">Путь к исходному файлу (doc, docx, etc)</param>
        /// <returns>Путь к PDF файлу</returns>
        public async Task<string> ConvertToPdfAsync(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath) || !System.IO.File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Исходный файл не найден", sourceFilePath);
            }

            // Создаем уникальную временную папку для операции
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string workDir = Path.Combine(_tempFolder, $"convert_{timestamp}_{uniqueId}");

            Directory.CreateDirectory(workDir);

            try
            {
                // Имя файла без расширения
                string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);

                // Копируем исходный файл во временную папку
                string tempSourcePath = Path.Combine(workDir, Path.GetFileName(sourceFilePath));
                System.IO.File.Copy(sourceFilePath, tempSourcePath, true);

                // Формируем путь для результата (PDF)
                string outputDir = Path.Combine(_outputBasePath, "PDF");
                Directory.CreateDirectory(outputDir);
                string pdfPath = Path.Combine(outputDir, $"{fileName}_{timestamp}.pdf");

                // Запускаем процесс конвертации через LibreOffice
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _libreOfficePath,
                    Arguments = $"--headless --convert-to pdf --outdir \"{outputDir}\" \"{tempSourcePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation($"Запуск конвертации в PDF: {processStartInfo.Arguments}");

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"Ошибка конвертации: {error}");
                    throw new Exception($"Ошибка при конвертации в PDF: {error}");
                }

                _logger.LogInformation($"Результат конвертации: {output}");

                // LibreOffice создает PDF с тем же именем, но с расширением .pdf
                string actualPdfPath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(tempSourcePath)}.pdf");

                if (!System.IO.File.Exists(actualPdfPath))
                {
                    _logger.LogError($"PDF файл не создан по пути: {actualPdfPath}");
                    throw new FileNotFoundException("PDF файл не был создан", actualPdfPath);
                }

                // Если путь не совпадает с ожидаемым, перемещаем файл
                if (actualPdfPath != pdfPath)
                {
                    if (System.IO.File.Exists(pdfPath))
                    {
                        System.IO.File.Delete(pdfPath);
                    }
                    System.IO.File.Move(actualPdfPath, pdfPath);
                }

                return pdfPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при конвертации в PDF: {ex.Message}");
                throw;
            }
            finally
            {
                // Очищаем временную папку
                try
                {
                    if (Directory.Exists(workDir))
                    {
                        Directory.Delete(workDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Не удалось очистить временную папку: {workDir}");
                }
            }
        }

        /// <summary>
        /// Конвертирует содержимое документа в PDF
        /// </summary>
        /// <param name="content">Содержимое документа</param>
        /// <param name="extension">Расширение файла (.doc, .docx и т.д.)</param>
        /// <returns>Содержимое PDF и путь к файлу</returns>
        public async Task<(byte[] Content, string FilePath)> ConvertContentToPdfAsync(byte[] content, string extension)
        {
            if (content == null || content.Length == 0)
            {
                throw new ArgumentException("Содержимое документа пустое", nameof(content));
            }

            if (string.IsNullOrEmpty(extension))
            {
                extension = ".docx";
            }

            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            // Создаем уникальную временную папку
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string workDir = Path.Combine(_tempFolder, $"convert_{timestamp}_{uniqueId}");

            Directory.CreateDirectory(workDir);

            try
            {
                // Сохраняем содержимое во временный файл
                string tempFilePath = Path.Combine(workDir, $"document_{timestamp}{extension}");
                await System.IO.File.WriteAllBytesAsync(tempFilePath, content);

                // Конвертируем в PDF
                string pdfPath = await ConvertToPdfAsync(tempFilePath);

                // Проверяем, что файл существует
                if (!System.IO.File.Exists(pdfPath))
                {
                    throw new FileNotFoundException("Сгенерированный PDF файл не найден", pdfPath);
                }

                // Читаем содержимое PDF
                byte[] pdfContent = await System.IO.File.ReadAllBytesAsync(pdfPath);

                return (pdfContent, pdfPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при конвертации содержимого в PDF");
                throw;
            }
            finally
            {
                // Очищаем временную папку
                try
                {
                    if (Directory.Exists(workDir))
                    {
                        Directory.Delete(workDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Не удалось очистить временную папку: {workDir}");
                }
            }
        }
    }
}