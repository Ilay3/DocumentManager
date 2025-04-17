
using DocumentManager.Infrastructure.Services;
namespace DocumentManager.Web.Models
{
    public class TemplateFilesViewModel
    {
        public List<TemplateFileInfo> WordTemplates { get; set; } = new List<TemplateFileInfo>();
        public List<TemplateFileInfo> JsonSchemas { get; set; } = new List<TemplateFileInfo>();
    }
}
