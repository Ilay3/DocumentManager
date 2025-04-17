namespace DocumentManager.Web.Models
{
    public class TemplatesAdminViewModel
    {
        public List<DocumentTemplateViewModel> Templates { get; set; } = new List<DocumentTemplateViewModel>();
        public string Filter { get; set; }
    }

}
