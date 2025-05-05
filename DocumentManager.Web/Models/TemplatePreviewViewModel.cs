using System;
using System.Collections.Generic;

namespace DocumentManager.Web.Models
{
    public class TemplatePreviewViewModel
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string TemplateCode { get; set; }
        public string TemplateType { get; set; }
        public string WordTemplatePath { get; set; }
        public Dictionary<string, string> TestValues { get; set; } = new Dictionary<string, string>();
        public List<DocumentFieldViewModel> Fields { get; set; } = new List<DocumentFieldViewModel>();
    }
}