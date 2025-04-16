using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentManager.Core.Interfaces
{
    /// <summary>
    /// Сервис для генерации документов
    /// </summary>
    public interface IDocumentGenerationService
    {
        /// <summary>
        /// Сгенерировать документ Word на основе шаблона и значений полей
        /// </summary>
        Task<(string FilePath, byte[] Content)> GenerateDocumentAsync(int documentId);

        /// <summary>
        /// Сгенерировать документ Word на основе шаблона и значений полей
        /// </summary>
        Task<(string FilePath, byte[] Content)> GenerateDocumentAsync(string templatePath, Dictionary<string, string> fieldValues, string outputFileName);

        /// <summary>
        /// Сгенерировать связанные документы (например, паспорт и упаковочные листы)
        /// </summary>
        Task<IEnumerable<(int DocumentId, string FilePath, byte[] Content)>> GenerateRelatedDocumentsAsync(int documentId);
    }
}