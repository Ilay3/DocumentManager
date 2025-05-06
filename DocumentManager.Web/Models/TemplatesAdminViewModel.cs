namespace DocumentManager.Web.Models
{
    public class TemplatesAdminViewModel
    {
        public List<DocumentTemplateViewModel> Templates { get; set; } = new List<DocumentTemplateViewModel>();
        public string SearchTerm { get; set; }
        public string TypeFilter { get; set; }
        public bool? IsActiveFilter { get; set; }
        public string SortBy { get; set; } = "Name";
        public string SortDirection { get; set; } = "asc";
        public List<string> AvailableTypes { get; set; } = new List<string>();
    }


}
