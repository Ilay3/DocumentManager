using DocumentManager.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Words.NET;

namespace DocumentManager.Infrastructure.Services
{
    public class DocumentGenerationService : IDocumentGenerationService
    {
        private readonly string _templatesBasePath;
        private readonly string _outputBasePath;
        private readonly IDocumentService _documentService;

        public DocumentGenerationService(
            string templatesBasePath,
            string outputBasePath,
            IDocumentService documentService)
        {
            _templatesBasePath = templatesBasePath;
            _outputBasePath = outputBasePath;
            _documentService = documentService;
        }

        public async Task<string> GenerateDocumentAsync(int documentId)
        {
            var document = await _documentService.GetDocumentByIdAsync(documentId);

            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found");
            }

            var templatePath = Path.Combine(_templatesBasePath, document.DocumentTemplate.WordTemplatePath);
            var fieldValues = await _documentService.GetDocumentValuesAsync(documentId);

            var outputFileName = $"{document.DocumentTemplate.Code}_{document.FactoryNumber}_{DateTime.Now:yyyyMMdd}.docx";

            return await GenerateDocumentAsync(templatePath, fieldValues, outputFileName);
        }

        public async Task<string> GenerateDocumentAsync(string templatePath, Dictionary<string, string> fieldValues, string outputFileName)
        {
            // Проверяем существование шаблона
            var fullTemplatePath = Path.Combine(_templatesBasePath, templatePath);

            if (!File.Exists(fullTemplatePath))
            {
                throw new FileNotFoundException($"Template file not found: {fullTemplatePath}");
            }

            // Создаем директорию для выходного файла, если не существует
            Directory.CreateDirectory(_outputBasePath);

            var outputPath = Path.Combine(_outputBasePath, outputFileName);

            // Копируем шаблон в выходной файл
            File.Copy(fullTemplatePath, outputPath, true);

            // Заменяем плейсхолдеры в документе
            using (var document = DocX.Load(outputPath))
            {
                foreach (var field in fieldValues)
                {
                    // Заменяем плейсхолдеры вида {{FieldName}}
                    document.ReplaceText($"{{{{{field.Key}}}}}", field.Value ?? string.Empty);
                }

                document.Save();
            }

            return outputPath;
        }

        public async Task<IEnumerable<string>> GenerateRelatedDocumentsAsync(int documentId)
        {
            var document = await _documentService.GetDocumentByIdAsync(documentId);

            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found");
            }

            var result = new List<string>
            {
                await GenerateDocumentAsync(documentId)
            };

            var relatedDocuments = await _documentService.GetRelatedDocumentsAsync(documentId);

            foreach (var relatedDocument in relatedDocuments)
            {
                result.Add(await GenerateDocumentAsync(relatedDocument.Id));
            }

            return result;
        }
    }

}
