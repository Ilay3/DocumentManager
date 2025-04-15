using DocumentManager.Core.Interfaces;
using DocumentManager.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManager.Web.Controllers
{
    public class TemplatesController : Controller
    {
        private readonly ITemplateService _templateService;

        public TemplatesController(ITemplateService templateService)
        {
            _templateService = templateService;
        }

        // GET: Templates
        public async Task<IActionResult> Index()
        {
            var templates = await _templateService.GetAllTemplatesAsync();

            var viewModels = templates.Select(t => new DocumentTemplateViewModel
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                Type = t.Type,
                IsActive = t.IsActive
            }).ToList();

            return View(viewModels);
        }

        // GET: Templates/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var template = await _templateService.GetTemplateByIdAsync(id);

            if (template == null)
            {
                return NotFound();
            }

            var fields = await _templateService.GetTemplateFieldsAsync(id);

            var viewModel = new DocumentFormViewModel
            {
                TemplateId = template.Id,
                TemplateCode = template.Code,
                TemplateName = template.Name,
                Fields = fields.Select(f => new DocumentFieldViewModel
                {
                    Id = f.Id,
                    FieldName = f.FieldName,
                    FieldLabel = f.FieldLabel,
                    FieldType = f.FieldType,
                    IsRequired = f.IsRequired,
                    IsUnique = f.IsUnique,
                    DefaultValue = f.DefaultValue
                }).ToList()
            };

            return View(viewModel);
        }
    }

}
