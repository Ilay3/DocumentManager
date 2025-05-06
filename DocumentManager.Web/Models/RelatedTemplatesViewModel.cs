namespace DocumentManager.Web.Models
{
    public class RelatedTemplatesViewModel
    {
        public DocumentTemplateViewModel MainTemplate { get; set; }
        public List<DocumentTemplateViewModel> RelatedTemplates { get; set; } = new List<DocumentTemplateViewModel>();
    }

}
