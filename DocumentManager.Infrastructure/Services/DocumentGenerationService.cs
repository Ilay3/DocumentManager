using DocumentManager.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xceed.Words.NET;
using System.Linq;

namespace DocumentManager.Infrastructure.Services
{
    public class DocumentGenerationService : IDocumentGenerationService
    {
        private readonly string _templatesBasePath;
        private readonly string _outputBasePath;
        private readonly IDocumentService _documentService;
        private readonly ILogger<DocumentGenerationService> _logger;

        public DocumentGenerationService(
            string templatesBasePath,
            string outputBasePath,
            IDocumentService documentService,
            ILogger<DocumentGenerationService> logger)
        {
            _templatesBasePath = templatesBasePath;
            _outputBasePath = outputBasePath;
            _documentService = documentService;
            _logger = logger;
        }

        public async Task<(string FilePath, byte[] Content)> GenerateDocumentAsync(int documentId)
        {
            var document = await _documentService.GetDocumentByIdAsync(documentId);

            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found");
            }

            var templatePath = document.DocumentTemplate.WordTemplatePath;
            var fieldValues = await _documentService.GetDocumentValuesAsync(documentId);

            _logger.LogInformation($"Генерация документа ID {documentId}, шаблон: {templatePath}, значения: {string.Join(", ", fieldValues.Select(kv => $"{kv.Key}={kv.Value}"))}");

            var extension = Path.GetExtension(templatePath).ToLowerInvariant();
            var outputFileName = $"{document.DocumentTemplate.Code}_{document.FactoryNumber}_{DateTime.Now:yyyyMMdd}{extension}";

            return await GenerateDocumentAsync(templatePath, fieldValues, outputFileName);
        }

        public async Task<(string FilePath, byte[] Content)> GenerateDocumentAsync(string templatePath, Dictionary<string, string> fieldValues, string outputFileName)
        {
            // Проверяем существование шаблона
            var fullTemplatePath = Path.IsPathRooted(templatePath) 
                ? templatePath 
                : Path.Combine(_templatesBasePath, templatePath);

            _logger.LogInformation($"Генерация документа из шаблона: {fullTemplatePath}");
            _logger.LogInformation($"Значения полей: {string.Join(", ", fieldValues.Select(kv => $"{kv.Key}={kv.Value}"))}");

            if (!File.Exists(fullTemplatePath))
            {
                _logger.LogError($"Файл шаблона не найден: {fullTemplatePath}");
                
                // Попробуем найти файл в других расширениях
                var directory = Path.GetDirectoryName(fullTemplatePath);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fullTemplatePath);
                var possibleFiles = Directory.GetFiles(directory, $"{fileNameWithoutExt}.*").ToList();
                
                if (possibleFiles.Any())
                {
                    _logger.LogInformation($"Найдены альтернативные файлы: {string.Join(", ", possibleFiles)}");
                    fullTemplatePath = possibleFiles.First();
                    _logger.LogInformation($"Используем файл: {fullTemplatePath}");
                }
                else
                {
                    throw new FileNotFoundException($"Файл шаблона не найден: {fullTemplatePath}");
                }
            }

            var extension = Path.GetExtension(fullTemplatePath).ToLowerInvariant();
            byte[] documentContent;
            
            // Handle different file formats
            if (extension == ".doc")
            {
                _logger.LogInformation("Обработка .doc файла с использованием бинарного подхода");
                documentContent = ProcessDocFile(fullTemplatePath, fieldValues);
            }
            else if (extension == ".docx")
            {
                _logger.LogInformation("Обработка .docx файла с использованием библиотеки DocX");
                documentContent = await ProcessDocxFile(fullTemplatePath, fieldValues);
            }
            else
            {
                _logger.LogError($"Неподдерживаемый формат файла: {extension}");
                throw new NotSupportedException($"Неподдерживаемый формат файла: {extension}");
            }

            // Create output directory if it doesn't exist
            if (!Directory.Exists(_outputBasePath))
            {
                _logger.LogInformation($"Создание директории вывода: {_outputBasePath}");
                Directory.CreateDirectory(_outputBasePath);
            }

            // Save to file system
            var outputPath = Path.Combine(_outputBasePath, outputFileName);
            _logger.LogInformation($"Сохранение сгенерированного документа в: {outputPath}");
            await File.WriteAllBytesAsync(outputPath, documentContent);

            return (outputPath, documentContent);
        }

        private byte[] ProcessDocFile(string templatePath, Dictionary<string, string> fieldValues)
        {
            try
            {
                _logger.LogInformation($"Обработка .doc файла: {templatePath}");
                var docHandler = new DocBinaryTemplateHandler(_logger);
                
                // Найдем все плейсхолдеры в шаблоне для диагностики
                var placeholders = docHandler.FindPlaceholders(templatePath);
                _logger.LogInformation($"Найдено {placeholders.Count} плейсхолдеров в шаблоне");
                
                foreach (var ph in placeholders)
                {
                    if (fieldValues.ContainsKey(ph))
                    {
                        _logger.LogInformation($"Плейсхолдер {ph} будет заменен на значение: {fieldValues[ph]}");
                    }
                    else
                    {
                        _logger.LogWarning($"Плейсхолдер {ph} найден в шаблоне, но значение для него не предоставлено");
                    }
                }
                
                return docHandler.ProcessTemplate(templatePath, fieldValues);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обработке .doc файла: {ex.Message}");
                throw;
            }
        }

        private async Task<byte[]> ProcessDocxFile(string templatePath, Dictionary<string, string> fieldValues)
        {
            // For .docx files we need to use a temporary file
            var tempFilePath = Path.GetTempFileName();
            try
            {
                _logger.LogInformation($"Обработка .docx файла: {templatePath}");
                _logger.LogInformation($"Используем временный файл: {tempFilePath}");
                
                // Copy template to temporary location to avoid modifying the original
                File.Copy(templatePath, tempFilePath, true);

                // Use DocX to replace placeholders
                using (var document = DocX.Load(tempFilePath))
                {
                    // Для диагностики - найдем все плейсхолдеры в тексте документа
                    var text = document.Text;
                    var regex = new System.Text.RegularExpressions.Regex(@"\{\{([^}]+)\}\}");
                    var matches = regex.Matches(text);
                    var placeholders = matches.Cast<System.Text.RegularExpressions.Match>()
                        .Where(m => m.Groups.Count > 1)
                        .Select(m => m.Groups[1].Value)
                        .Distinct()
                        .ToList();
                    
                    _logger.LogInformation($"Найдено {placeholders.Count} плейсхолдеров в .docx файле: {string.Join(", ", placeholders)}");
                    
                    foreach (var field in fieldValues)
                    {
                        // Replace placeholders of the form {{FieldName}}
                        string placeholder = $"{{{{{field.Key}}}}}";
                        string value = field.Value ?? string.Empty;
                        
                        if (text.Contains(placeholder))
                        {
                            _logger.LogInformation($"Замена '{placeholder}' на '{value}'");
                            document.ReplaceText(placeholder, value);
                        }
                        else
                        {
                            _logger.LogWarning($"Плейсхолдер '{placeholder}' не найден в документе");
                        }
                    }

                    document.Save();
                }

                // Read the processed document into memory
                var result = await File.ReadAllBytesAsync(tempFilePath);
                _logger.LogInformation($"Документ успешно обработан, размер: {result.Length} байт");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обработке .docx файла: {ex.Message}");
                throw;
            }
            finally
            {
                // Always clean up the temporary file
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                        _logger.LogInformation($"Временный файл удален: {tempFilePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Не удалось удалить временный файл: {tempFilePath}");
                    }
                }
            }
        }

        public async Task<IEnumerable<(int DocumentId, string FilePath, byte[] Content)>> GenerateRelatedDocumentsAsync(int documentId)
        {
            var document = await _documentService.GetDocumentByIdAsync(documentId);

            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found");
            }

            var result = new List<(int DocumentId, string FilePath, byte[] Content)>();
            
            // Generate main document
            try
            {
                _logger.LogInformation($"Генерация основного документа ID: {documentId}");
                var (mainPath, mainContent) = await GenerateDocumentAsync(documentId);
                // Update the document with content
                await _documentService.UpdateDocumentContentAsync(documentId, mainContent, mainPath);
                result.Add((documentId, mainPath, mainContent));
                _logger.LogInformation($"Основной документ успешно сгенерирован: {mainPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка генерации основного документа ID {documentId}: {ex.Message}");
                // Continue with related documents even if main fails
            }
            
            // Generate related documents
            var relatedDocuments = await _documentService.GetRelatedDocumentsAsync(documentId);
            _logger.LogInformation($"Найдено {relatedDocuments.Count()} связанных документов");
            
            foreach (var relatedDocument in relatedDocuments)
            {
                try
                {
                    _logger.LogInformation($"Генерация связанного документа ID: {relatedDocument.Id}");
                    var (relatedPath, relatedContent) = await GenerateDocumentAsync(relatedDocument.Id);
                    // Update the document with content
                    await _documentService.UpdateDocumentContentAsync(relatedDocument.Id, relatedContent, relatedPath);
                    result.Add((relatedDocument.Id, relatedPath, relatedContent));
                    _logger.LogInformation($"Связанный документ успешно сгенерирован: {relatedPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка генерации связанного документа ID {relatedDocument.Id}: {ex.Message}");
                    // Continue with other documents
                }
            }

            return result;
        }
    }
}