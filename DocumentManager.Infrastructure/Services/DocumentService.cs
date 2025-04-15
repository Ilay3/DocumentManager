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
    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _context;

        public DocumentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Document>> GetAllDocumentsAsync()
        {
            return await _context.Documents
                .Include(d => d.DocumentTemplate)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<Document> GetDocumentByIdAsync(int id)
        {
            return await _context.Documents
                .Include(d => d.DocumentTemplate)
                .Include(d => d.Values)
                    .ThenInclude(v => v.DocumentField)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<IEnumerable<Document>> GetDocumentsByTemplateAsync(int templateId)
        {
            return await _context.Documents
                .Where(d => d.DocumentTemplateId == templateId)
                .Include(d => d.DocumentTemplate)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> IsFactoryNumberUniqueAsync(int templateId, string factoryNumber, int? excludeDocumentId = null)
        {
            var query = _context.Documents
                .Where(d => d.DocumentTemplateId == templateId && d.FactoryNumber == factoryNumber);

            if (excludeDocumentId.HasValue)
            {
                query = query.Where(d => d.Id != excludeDocumentId.Value);
            }

            return !await query.AnyAsync();
        }

        public async Task<Document> CreateDocumentAsync(Document document, Dictionary<string, string> fieldValues)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _context.Documents.Add(document);
                    await _context.SaveChangesAsync();

                    var fields = await _context.DocumentFields
                        .Where(f => f.DocumentTemplateId == document.DocumentTemplateId)
                        .ToListAsync();

                    foreach (var field in fields)
                    {
                        if (fieldValues.TryGetValue(field.FieldName, out var value))
                        {
                            var documentValue = new DocumentValue
                            {
                                DocumentId = document.Id,
                                DocumentFieldId = field.Id,
                                Value = value
                            };

                            _context.DocumentValues.Add(documentValue);
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return document;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        public async Task<bool> UpdateDocumentAsync(Document document, Dictionary<string, string> fieldValues)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _context.Entry(document).State = EntityState.Modified;

                    // Удаляем существующие значения полей
                    var existingValues = await _context.DocumentValues
                        .Where(v => v.DocumentId == document.Id)
                        .ToListAsync();

                    _context.DocumentValues.RemoveRange(existingValues);

                    // Добавляем новые значения полей
                    var fields = await _context.DocumentFields
                        .Where(f => f.DocumentTemplateId == document.DocumentTemplateId)
                        .ToListAsync();

                    foreach (var field in fields)
                    {
                        if (fieldValues.TryGetValue(field.FieldName, out var value))
                        {
                            var documentValue = new DocumentValue
                            {
                                DocumentId = document.Id,
                                DocumentFieldId = field.Id,
                                Value = value
                            };

                            _context.DocumentValues.Add(documentValue);
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        public async Task<bool> DeleteDocumentAsync(int id)
        {
            var document = await _context.Documents.FindAsync(id);

            if (document == null)
            {
                return false;
            }

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<Dictionary<string, string>> GetDocumentValuesAsync(int documentId)
        {
            var values = await _context.DocumentValues
                .Include(v => v.DocumentField)
                .Where(v => v.DocumentId == documentId)
                .ToListAsync();

            var result = new Dictionary<string, string>();

            foreach (var value in values)
            {
                result[value.DocumentField.FieldName] = value.Value;
            }

            return result;
        }

        public async Task<bool> RelateDocumentsAsync(int parentDocumentId, int childDocumentId)
        {
            var relation = new DocumentRelation
            {
                ParentDocumentId = parentDocumentId,
                ChildDocumentId = childDocumentId
            };

            _context.DocumentRelations.Add(relation);

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IEnumerable<Document>> GetRelatedDocumentsAsync(int documentId)
        {
            var relations = await _context.DocumentRelations
                .Where(r => r.ParentDocumentId == documentId)
                .Include(r => r.ChildDocument)
                    .ThenInclude(d => d.DocumentTemplate)
                .ToListAsync();

            return relations.Select(r => r.ChildDocument);
        }
    }

}
