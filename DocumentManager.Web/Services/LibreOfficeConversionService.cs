using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
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
        private readonly int _conversionTimeoutSeconds;
        private static readonly SemaphoreSlim _conversionSemaphore = new SemaphoreSlim(1, 1); // Prevent multiple concurrent conversions

        public LibreOfficeConversionService(
            ILogger<LibreOfficeConversionService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _libreOfficePath = configuration["LibreOffice:Path"] ?? @"C:\Program Files\LibreOffice\program\soffice.exe";
            _tempFolder = configuration["LibreOffice:TempFolder"] ?? Path.GetTempPath();
            _outputBasePath = configuration["OutputBasePath"] ?? Path.GetTempPath();
            _conversionTimeoutSeconds = int.TryParse(configuration["LibreOffice:TimeoutSeconds"], out int timeout) ? timeout : 60;

            // Log key configuration parameters
            _logger.LogInformation($"LibreOffice path: {_libreOfficePath}");
            _logger.LogInformation($"Temp folder: {_tempFolder}");
            _logger.LogInformation($"Output base path: {_outputBasePath}");
            _logger.LogInformation($"Conversion timeout: {_conversionTimeoutSeconds} seconds");

            // Ensure required directories exist
            EnsureDirectoryExists(_tempFolder);
            EnsureDirectoryExists(_outputBasePath);
            EnsureDirectoryExists(Path.Combine(_outputBasePath, "PDF"));

            // Check if LibreOffice exists
            if (!File.Exists(_libreOfficePath))
            {
                _logger.LogWarning($"⚠️ LibreOffice not found at path: {_libreOfficePath}");
                // Try to find it in alternative locations
                var possiblePaths = new[] {
                    @"C:\Program Files\LibreOffice\program\soffice.exe",
                    @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        _libreOfficePath = path;
                        _logger.LogInformation($"Found LibreOffice at alternative path: {_libreOfficePath}");
                        break;
                    }
                }
            }
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                    _logger.LogInformation($"Created directory: {path}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to create directory: {path}");
                }
            }
        }

        /// <summary>
        /// Конвертирует документ в PDF
        /// </summary>
        /// <param name="sourceFilePath">Путь к исходному файлу (doc, docx, etc)</param>
        /// <returns>Путь к PDF файлу</returns>
        public async Task<string> ConvertToPdfAsync(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Source file not found", sourceFilePath);
            }

            // Ensure file path is absolute
            sourceFilePath = Path.GetFullPath(sourceFilePath);
            _logger.LogInformation($"Converting to PDF: {sourceFilePath}");

            // Create unique working directory
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string workDir = Path.Combine(_tempFolder, $"convert_{timestamp}_{uniqueId}");

            try
            {
                Directory.CreateDirectory(workDir);
                _logger.LogDebug($"Created working directory: {workDir}");

                // Copy source file to working directory
                string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
                string tempSourcePath = Path.Combine(workDir, Path.GetFileName(sourceFilePath));
                File.Copy(sourceFilePath, tempSourcePath, true);
                _logger.LogDebug($"Copied source file to: {tempSourcePath}");

                // Create output directory
                string outputDir = Path.Combine(_outputBasePath, "PDF");
                Directory.CreateDirectory(outputDir);

                // Generate PDF file path
                string pdfFileName = $"{fileName}_{timestamp}.pdf";
                string pdfPath = Path.Combine(outputDir, pdfFileName);
                _logger.LogDebug($"PDF output path: {pdfPath}");

                // Kill any hanging LibreOffice processes before starting
                await KillHangingProcessesAsync();

                // Lock to prevent multiple LibreOffice processes
                await _conversionSemaphore.WaitAsync();

                try
                {
                    // Configure process
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = _libreOfficePath,
                        Arguments = $"--headless --convert-to pdf --outdir \"{outputDir}\" \"{tempSourcePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = workDir // Set working directory explicitly
                    };

                    _logger.LogInformation($"Starting LibreOffice process: {processStartInfo.FileName} {processStartInfo.Arguments}");

                    using var process = new Process { StartInfo = processStartInfo };

                    // Add event handlers to capture output in real-time
                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, args) => {
                        if (args.Data != null)
                        {
                            outputBuilder.AppendLine(args.Data);
                            _logger.LogDebug($"LibreOffice output: {args.Data}");
                        }
                    };

                    process.ErrorDataReceived += (sender, args) => {
                        if (args.Data != null)
                        {
                            errorBuilder.AppendLine(args.Data);
                            _logger.LogWarning($"LibreOffice error: {args.Data}");
                        }
                    };

                    // Start the process and begin reading output
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Wait for exit with timeout
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_conversionTimeoutSeconds));
                    var processTask = process.WaitForExitAsync();

                    // Wait for either process completion or timeout
                    if (await Task.WhenAny(processTask, timeoutTask) == timeoutTask)
                    {
                        // Process timed out
                        _logger.LogError($"LibreOffice conversion timed out after {_conversionTimeoutSeconds} seconds");

                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill(true); // Kill process tree
                                _logger.LogWarning("Killed hanging LibreOffice process");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to kill LibreOffice process");
                        }

                        throw new TimeoutException($"PDF conversion timed out after {_conversionTimeoutSeconds} seconds");
                    }

                    string output = outputBuilder.ToString();
                    string error = errorBuilder.ToString();

                    if (process.ExitCode != 0)
                    {
                        _logger.LogError($"LibreOffice conversion failed with exit code {process.ExitCode}");
                        _logger.LogError($"Error output: {error}");
                        throw new Exception($"PDF conversion failed with exit code {process.ExitCode}: {error}");
                    }

                    _logger.LogInformation($"LibreOffice conversion completed successfully");
                    _logger.LogDebug($"Conversion output: {output}");

                    // LibreOffice creates PDF with the same name, but with extension .pdf
                    string actualPdfPath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(tempSourcePath)}.pdf");

                    if (!File.Exists(actualPdfPath))
                    {
                        _logger.LogError($"PDF file not created: {actualPdfPath}");

                        // Look for any PDF in the output directory
                        var pdfFiles = Directory.GetFiles(outputDir, "*.pdf");
                        if (pdfFiles.Length > 0)
                        {
                            _logger.LogWarning($"Found alternative PDF: {pdfFiles[0]}");
                            actualPdfPath = pdfFiles[0];
                        }
                        else
                        {
                            throw new FileNotFoundException("PDF file was not created by LibreOffice", actualPdfPath);
                        }
                    }

                    // Rename if necessary
                    if (actualPdfPath != pdfPath)
                    {
                        if (File.Exists(pdfPath))
                        {
                            File.Delete(pdfPath);
                        }
                        File.Move(actualPdfPath, pdfPath);
                        _logger.LogDebug($"Renamed PDF file: {actualPdfPath} -> {pdfPath}");
                    }

                    return pdfPath;
                }
                finally
                {
                    // Always release the lock
                    _conversionSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during PDF conversion: {ex.Message}");
                throw;
            }
            finally
            {
                // Cleanup working directory
                try
                {
                    if (Directory.Exists(workDir))
                    {
                        Directory.Delete(workDir, true);
                        _logger.LogDebug($"Cleaned up temporary directory: {workDir}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to clean up temporary directory: {workDir}");
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
                throw new ArgumentException("Document content is empty", nameof(content));
            }

            if (string.IsNullOrEmpty(extension))
            {
                extension = ".docx";  // Default extension
            }

            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            // Create temporary directory
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string workDir = Path.Combine(_tempFolder, $"convert_{timestamp}_{uniqueId}");

            try
            {
                Directory.CreateDirectory(workDir);
                _logger.LogDebug($"Created temporary directory for content conversion: {workDir}");

                // Save content to temporary file
                string tempFilePath = Path.Combine(workDir, $"document_{timestamp}{extension}");
                await File.WriteAllBytesAsync(tempFilePath, content);
                _logger.LogDebug($"Saved content to temporary file: {tempFilePath}");

                try
                {
                    // Convert to PDF
                    string pdfPath = await ConvertToPdfAsync(tempFilePath);
                    _logger.LogInformation($"Successfully converted content to PDF: {pdfPath}");

                    // Read PDF content
                    byte[] pdfContent = await File.ReadAllBytesAsync(pdfPath);
                    _logger.LogDebug($"Read PDF content, size: {pdfContent.Length} bytes");

                    return (pdfContent, pdfPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to convert content to PDF");

                    // If conversion fails, try to return the original document
                    _logger.LogWarning("Attempting to return original document instead of PDF");
                    return (content, tempFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in content to PDF conversion");
                throw;
            }
            finally
            {
                // Clean up temporary directory
                try
                {
                    if (Directory.Exists(workDir))
                    {
                        Directory.Delete(workDir, true);
                        _logger.LogDebug($"Cleaned up temporary directory: {workDir}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to clean up temporary directory: {workDir}");
                }
            }
        }

        /// <summary>
        /// Kills any hanging LibreOffice processes
        /// </summary>
        private async Task KillHangingProcessesAsync()
        {
            try
            {
                // Find LibreOffice processes
                Process[] processes = Process.GetProcessesByName("soffice");
                if (processes.Length > 0)
                {
                    _logger.LogWarning($"Found {processes.Length} LibreOffice processes running. Attempting to kill them...");

                    foreach (var proc in processes)
                    {
                        try
                        {
                            // Check if the process has been running for too long
                            if ((DateTime.Now - proc.StartTime).TotalMinutes > 5)
                            {
                                _logger.LogWarning($"Killing hanging LibreOffice process (ID: {proc.Id}, Started: {proc.StartTime})");
                                proc.Kill(true);
                                await Task.Delay(500); // Give it time to clean up
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to kill LibreOffice process ID {proc.Id}");
                        }
                        finally
                        {
                            proc.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking for hanging LibreOffice processes");
            }
        }
    }
}