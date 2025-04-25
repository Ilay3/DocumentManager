namespace DocumentManager.Web.Models
{
    // Добавьте следующие свойства в класс DocumentViewModel
    public class DocumentViewModel
    {
        // Существующие свойства...
        public int Id { get; set; }
        public int TemplateId { get; set; }
        public string TemplateCode { get; set; }
        public string TemplateName { get; set; }
        public string FactoryNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string GeneratedFilePath { get; set; }
        public Dictionary<string, string> FieldValues { get; set; } = new Dictionary<string, string>();
        public List<DocumentViewModel> RelatedDocuments { get; set; } = new List<DocumentViewModel>();
        public List<DocumentFieldViewModel> DocumentFields { get; set; } = new List<DocumentFieldViewModel>();

        // Новые свойства для группировки
        public bool IsMainDocument { get; set; } = true; // По умолчанию документ основной
        public bool HasRelatedDocuments => RelatedDocuments?.Any() == true;
        public int RelatedDocumentsCount => RelatedDocuments?.Count ?? 0;
        public int? ParentDocumentId { get; set; } // ID родительского документа, если это связанный документ
        public string DocumentGroupId { get; set; } // Идентификатор группы документов (обычно равен FactoryNumber)
    }

}
