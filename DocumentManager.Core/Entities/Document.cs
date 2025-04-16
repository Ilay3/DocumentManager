
namespace DocumentManager.Core.Entities
{
    /// <summary>
    /// Созданный документ
    /// </summary>
    public class Document
    {
        public int Id { get; set; }
        public int DocumentTemplateId { get; set; }
        public string FactoryNumber { get; set; } // Заводской номер (ключевое поле)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; }
        public string? GeneratedFilePath { get; set; } // Путь к сгенерированному документу
        public byte[]? DocumentContent { get; set; } // Содержимое документа в бинарном виде

        // Навигационные свойства
        public DocumentTemplate DocumentTemplate { get; set; }
        public List<DocumentValue> Values { get; set; } = new List<DocumentValue>();
        public List<DocumentRelation> RelatedDocuments { get; set; } = new List<DocumentRelation>();
    }
}