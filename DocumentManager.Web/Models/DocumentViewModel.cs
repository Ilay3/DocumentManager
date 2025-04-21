namespace DocumentManager.Web.Models
{
    public class DocumentViewModel
    {
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

    }

}
