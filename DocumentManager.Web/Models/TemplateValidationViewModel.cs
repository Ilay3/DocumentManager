namespace DocumentManager.Web.Models
{
    public class TemplateValidationViewModel
    {
        public DocumentTemplateViewModel Template { get; set; }
        public bool TemplateExists { get; set; }
        public List<DocumentFieldViewModel> Fields { get; set; } = new List<DocumentFieldViewModel>();
        public List<string> Placeholders { get; set; } = new List<string>();
        public List<string> MissingFields { get; set; } = new List<string>();
        public List<string> UnusedFields { get; set; } = new List<string>();
        public bool IsValid { get; set; }
    }

}
