// DocumentManager.Web/Models/DocumentTemplateViewModel.cs
using DocumentManager.Web.Helpers;

namespace DocumentManager.Web.Models
{
    public class DocumentTemplateViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsActive { get; set; }
        public string JsonSchemaPath { get; set; }
        public string WordTemplatePath { get; set; }


        public string GetLocalizedType()
        {
            return LocalizationHelper.GetLocalizedDocumentType(Type);
        }

    }
}