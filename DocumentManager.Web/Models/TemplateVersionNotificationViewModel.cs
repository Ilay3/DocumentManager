// DocumentManager.Web/Models/TemplateVersionViewModel.cs
namespace DocumentManager.Web.Models
{
    public class TemplateVersionNotificationViewModel
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UpdatesCount { get; set; }
        public List<TemplateVersionUpdateViewModel> Updates { get; set; }
    }

    public class TemplateVersionUpdateViewModel
    {
        public string TemplateCode { get; set; }
        public string TemplateName { get; set; }
        public int? OldVersion { get; set; }
        public int? NewVersion { get; set; }
        public string OldPath { get; set; }
        public string NewPath { get; set; }
    }
}