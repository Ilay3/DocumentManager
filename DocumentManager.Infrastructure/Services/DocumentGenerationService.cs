using DocumentManager.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xceed.Words.NET;

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

            // Выбираем метод обработки в зависимости от расширения файла
            if (extension == ".docx")
            {
                _logger.LogInformation("Обработка файла DOCX");
                documentContent = await ProcessDocxFileImproved(fullTemplatePath, fieldValues);
            }
            else
            {
                _logger.LogInformation("Обработка файла DOC");
                var docHandler = new DocBinaryTemplateHandler(_logger);
                documentContent = docHandler.ProcessTemplate(fullTemplatePath, fieldValues);
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

        private async Task<byte[]> ProcessDocxFileImproved(string templatePath, Dictionary<string, string> fieldValues)
        {
            // Создаем временную копию шаблона
            var tempPath = Path.GetTempFileName();
            File.Delete(tempPath); // Удаляем файл, который создал GetTempFileName
            tempPath = Path.ChangeExtension(tempPath, ".docx"); // Добавляем расширение .docx

            try
            {
                _logger.LogInformation($"Создание временной копии шаблона: {tempPath}");
                File.Copy(templatePath, tempPath);

                // Заменяем плейсхолдеры в документе
                _logger.LogInformation("Замена плейсхолдеров в документе");

                using (var doc = DocX.Load(tempPath))
                {
                    _logger.LogInformation($"Документ загружен, количество параграфов: {doc.Paragraphs.Count}");

                    // Простой способ логирования содержимого для отладки
                    var documentText = doc.Text;
                    _logger.LogInformation($"Текст документа (первые 200 символов): {(documentText.Length > 200 ? documentText.Substring(0, 200) : documentText)}");

                    // Поиск всех плейсхолдеров в документе с помощью регулярных выражений
                    var placeholderRegex = new Regex(@"\{\{([^}]+)\}\}");
                    var matches = placeholderRegex.Matches(documentText);

                    _logger.LogInformation($"Найдено {matches.Count} плейсхолдеров в документе");

                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var placeholderName = match.Groups[1].Value;
                            _logger.LogInformation($"Найден плейсхолдер: {placeholderName}");

                            if (fieldValues.TryGetValue(placeholderName, out var value))
                            {
                                _logger.LogInformation($"Заменяем плейсхолдер {{{{${placeholderName}}}}} на '{value}'");

                                // Заменяем плейсхолдер во всех параграфах
                                foreach (var paragraph in doc.Paragraphs)
                                {
                                    if (paragraph.Text.Contains($"{{{{{placeholderName}}}}}"))
                                    {
                                        // Обнаружен плейсхолдер в параграфе
                                        _logger.LogInformation($"Заменяем плейсхолдер в параграфе: {paragraph.Text}");
                                        paragraph.ReplaceText($"{{{{{placeholderName}}}}}", value ?? string.Empty);
                                    }
                                }

                                // Также ищем и заменяем в таблицах
                                foreach (var table in doc.Tables)
                                {
                                    foreach (var row in table.Rows)
                                    {
                                        foreach (var cell in row.Cells)
                                        {
                                            foreach (var paragraph in cell.Paragraphs)
                                            {
                                                if (paragraph.Text.Contains($"{{{{{placeholderName}}}}}"))
                                                {
                                                    _logger.LogInformation($"Заменяем плейсхолдер в ячейке таблицы: {paragraph.Text}");
                                                    paragraph.ReplaceText($"{{{{{placeholderName}}}}}", value ?? string.Empty);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"Значение для плейсхолдера '{placeholderName}' не найдено в словаре");
                            }
                        }
                    }

                    // Сохраняем изменения
                    _logger.LogInformation("Сохранение изменений в документе");
                    doc.Save();
                }

                // Читаем результат
                _logger.LogInformation("Чтение результата");
                var result = await File.ReadAllBytesAsync(tempPath);
                _logger.LogInformation($"Документ успешно обработан, размер: {result.Length} байт");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обработке DOCX файла: {ex.Message}");
                throw;
            }
            finally
            {
                // Удаляем временный файл
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                        _logger.LogInformation($"Временный файл удален: {tempPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Не удалось удалить временный файл: {ex.Message}");
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