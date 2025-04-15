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
        Task<string> GenerateDocumentAsync(int documentId);

        /// <summary>
        /// Сгенерировать документ Word на основе шаблона и значений полей
        /// </summary>
        Task<string> GenerateDocumentAsync(string templatePath, Dictionary<string, string> fieldValues, string outputFileName);

        /// <summary>
        /// Сгенерировать связанные документы (например, паспорт и упаковочные листы)
        /// </summary>
        Task<IEnumerable<string>> GenerateRelatedDocumentsAsync(int documentId);
    }

}
