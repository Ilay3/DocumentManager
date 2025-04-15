using DocumentManager.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentManager.Core.Interfaces
{
    /// <summary>
    /// Сервис для работы с документами
    /// </summary>
    public interface IDocumentService
    {
        /// <summary>
        /// Получить все документы
        /// </summary>
        Task<IEnumerable<Document>> GetAllDocumentsAsync();

        /// <summary>
        /// Получить документ по идентификатору
        /// </summary>
        Task<Document> GetDocumentByIdAsync(int id);

        /// <summary>
        /// Получить документы по шаблону
        /// </summary>
        Task<IEnumerable<Document>> GetDocumentsByTemplateAsync(int templateId);

        /// <summary>
        /// Проверить уникальность заводского номера
        /// </summary>
        Task<bool> IsFactoryNumberUniqueAsync(int templateId, string factoryNumber, int? excludeDocumentId = null);

        /// <summary>
        /// Создать новый документ
        /// </summary>
        Task<Document> CreateDocumentAsync(Document document, Dictionary<string, string> fieldValues);

        /// <summary>
        /// Обновить документ
        /// </summary>
        Task<bool> UpdateDocumentAsync(Document document, Dictionary<string, string> fieldValues);

        /// <summary>
        /// Удалить документ
        /// </summary>
        Task<bool> DeleteDocumentAsync(int id);

        /// <summary>
        /// Получить значения полей документа
        /// </summary>
        Task<Dictionary<string, string>> GetDocumentValuesAsync(int documentId);

        /// <summary>
        /// Связать документы (например, паспорт и упаковочные листы)
        /// </summary>
        Task<bool> RelateDocumentsAsync(int parentDocumentId, int childDocumentId);

        /// <summary>
        /// Получить связанные документы
        /// </summary>
        Task<IEnumerable<Document>> GetRelatedDocumentsAsync(int documentId);
    }

}
