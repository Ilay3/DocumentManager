using DocumentManager.Core.Entities;
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentManager.Infrastructure.Services
{
    public class TemplateService : ITemplateService
    {
        private readonly ApplicationDbContext _context;
        private readonly IJsonSchemaService _jsonSchemaService;

        public TemplateService(ApplicationDbContext context, IJsonSchemaService jsonSchemaService)
        {
            _context = context;
            _jsonSchemaService = jsonSchemaService;
        }

        public async Task<IEnumerable<DocumentTemplate>> GetAllTemplatesAsync(bool includeInactive = false)
        {
            // Если includeInactive = true, возвращаем все шаблоны, иначе только активные
            var query = _context.DocumentTemplates.AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(t => t.IsActive);
            }

            return await query.OrderBy(t => t.Name).ToListAsync();
        }

        public async Task<DocumentTemplate> GetTemplateByIdAsync(int id)
        {
            return await _context.DocumentTemplates
                .Include(t => t.Fields)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<DocumentTemplate> GetTemplateByCodeAsync(string code)
        {
            return await _context.DocumentTemplates
                .Include(t => t.Fields)
                .FirstOrDefaultAsync(t => t.Code == code);
        }

        public async Task<IEnumerable<DocumentField>> GetTemplateFieldsAsync(int templateId)
        {
            return await _context.DocumentFields
                .Where(f => f.DocumentTemplateId == templateId)
                .OrderBy(f => f.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<DocumentField>> LoadFieldsFromJsonAsync(string jsonPath)
        {
            var schema = await _jsonSchemaService.LoadJsonSchemaAsync(jsonPath);
            var fields = _jsonSchemaService.GetFieldsFromSchema(schema);

            var result = new List<DocumentField>();

            foreach (var field in fields)
            {
                var documentField = new DocumentField
                {
                    FieldName = field.TryGetValue("fieldName", out var fieldName) ? fieldName.ToString() : null,
                    FieldLabel = field.TryGetValue("fieldLabel", out var fieldLabel) ? fieldLabel.ToString() : null,
                    FieldType = field.TryGetValue("fieldType", out var fieldType) ? fieldType.ToString() : null,
                    IsRequired = field.TryGetValue("isRequired", out var isRequired) && (bool)isRequired,
                    IsUnique = field.TryGetValue("isUnique", out var isUnique) && (bool)isUnique,
                    DefaultValue = field.TryGetValue("defaultValue", out var defaultValue) ? defaultValue.ToString() : null
                };

                if (field.TryGetValue("options", out var options))
                {
                    documentField.Options = System.Text.Json.JsonSerializer.Serialize(options);
                }

                if (field.TryGetValue("condition", out var condition))
                {
                    documentField.Condition = System.Text.Json.JsonSerializer.Serialize(condition);
                }

                result.Add(documentField);
            }

            return result;
        }

        public async Task<DocumentTemplate> AddTemplateAsync(DocumentTemplate template)
        {
            _context.DocumentTemplates.Add(template);
            await _context.SaveChangesAsync();
            return template;
        }

        public async Task<bool> UpdateTemplateAsync(DocumentTemplate template)
        {
            _context.Entry(template).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.DocumentTemplates.AnyAsync(e => e.Id == template.Id))
                {
                    return false;
                }

                throw;
            }
        }

        public async Task<bool> DeleteTemplateAsync(int id)
        {
            var template = await _context.DocumentTemplates.FindAsync(id);

            if (template == null)
            {
                return false;
            }

            template.IsActive = false;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}

