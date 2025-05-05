namespace DocumentManager.Web.Models
{
    public class DocumentFormViewModel
    {
        public int TemplateId { get; set; }
        public string TemplateCode { get; set; }
        public string TemplateName { get; set; }
        public List<DocumentFieldViewModel> Fields { get; set; } = new List<DocumentFieldViewModel>();
        public List<DocumentTemplateViewModel> RelatedTemplates { get; set; } = new List<DocumentTemplateViewModel>();
        public List<int> SelectedRelatedTemplateIds { get; set; } = new List<int>();

        public bool IsFactoryNumberDuplicate { get; set; } = false;
        public string FactoryNumber { get; set; }

    }

}
