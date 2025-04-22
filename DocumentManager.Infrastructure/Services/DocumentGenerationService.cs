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
                throw new ArgumentException($"Документ с ID {documentId} не найден");
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
            try
            {
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обработке шаблона {fullTemplatePath}: {ex.Message}");
                throw;
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
            // Создаём временный .docx
            var tempPath = Path.GetTempFileName();
            File.Delete(tempPath);
            tempPath = Path.ChangeExtension(tempPath, ".docx");

            try
            {
                _logger.LogInformation($"[ProcessDocx] Копируем шаблон {templatePath} → {tempPath}");
                File.Copy(templatePath, tempPath, overwrite: true);

                using (var doc = DocX.Load(tempPath))
                {
                    // Краткий превью текста
                    var preview = doc.Text.Length > 200
                        ? doc.Text.Substring(0, 200)
                        : doc.Text;
                    _logger.LogInformation($"[ProcessDocx] Текст документа (первые 200 символов):\n{preview}");

                    int totalReplacements = 0;

                    foreach (var kv in fieldValues)
                    {
                        var name = kv.Key;
                        var value = kv.Value ?? "";

                        // Четыре формата плейсхолдеров
                        var placeholders = new[]
                        {
                    "{{" + name + "}}",
                    "<<" + name + ">>",
                    "["  + name + "]",
                    "$"  + name
                };

                        foreach (var ph in placeholders)
                        {
                            // Считаем, сколько раз реально встречается в документе
                            var pattern = System.Text.RegularExpressions.Regex.Escape(ph);
                            int count = System.Text.RegularExpressions.Regex.Matches(doc.Text, pattern).Count;
                            if (count > 0)
                            {
                                // Заменяем все вхождения
                                doc.ReplaceText(ph, value, false, System.Text.RegularExpressions.RegexOptions.None);
                                totalReplacements += count;
                                _logger.LogInformation($"[ProcessDocx] Заменено {count}× «{ph}» → «{value}»");
                            }
                        }
                    }

                    _logger.LogInformation($"[ProcessDocx] Всего заменено: {totalReplacements}. Сохраняем документ.");
                    doc.Save();
                }

                _logger.LogInformation("[ProcessDocx] Чтение обработанного файла");
                var result = await File.ReadAllBytesAsync(tempPath);
                _logger.LogInformation($"[ProcessDocx] Возвращаем документ размером {result.Length} байт");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ProcessDocx] Ошибка при обработке шаблона {templatePath}");
                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                        _logger.LogInformation($"[ProcessDocx] Удалён временный файл {tempPath}");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, $"[ProcessDocx] Не удалось удалить временный файл {tempPath}");
                }
            }
        }




        public async Task<IEnumerable<(int DocumentId, string FilePath, byte[] Content)>> GenerateRelatedDocumentsAsync(int documentId)
        {
            var document = await _documentService.GetDocumentByIdAsync(documentId);

            if (document == null)
            {
                throw new ArgumentException($"Документ с ID {documentId} не найден");
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
            }

            // Generate related documents
            try
            {
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
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обработке связанных документов: {ex.Message}");
            }

            return result;
        }
    }
}