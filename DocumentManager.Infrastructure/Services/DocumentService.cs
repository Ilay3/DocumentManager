using DocumentManager.Core.Entities;
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentManager.Infrastructure.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DocumentService> _logger;

        // Используем фабрику сервисов вместо прямого внедрения контекста
        public DocumentService(IServiceScopeFactory scopeFactory, ILogger<DocumentService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<IEnumerable<Document>> GetAllDocumentsAsync()
        {
            _logger.LogInformation("Получение всех документов");

            // Создаем новую область видимости для каждой операции
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                return await context.Documents
                    .Include(d => d.DocumentTemplate)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();
            }
        }

        public async Task<Document> GetDocumentByIdAsync(int id)
        {
            _logger.LogInformation($"Получение документа по ID: {id}");

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    return await context.Documents
                        .Include(d => d.DocumentTemplate)
                        .Include(d => d.Values)
                            .ThenInclude(v => v.DocumentField)
                        .FirstOrDefaultAsync(d => d.Id == id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении документа ID {id}");
                throw;
            }
        }

        public async Task<IEnumerable<Document>> GetDocumentsByTemplateAsync(int templateId)
        {
            _logger.LogInformation($"Получение документов по шаблону ID: {templateId}");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                return await context.Documents
                    .Where(d => d.DocumentTemplateId == templateId)
                    .Include(d => d.DocumentTemplate)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();
            }
        }

        public async Task<bool> IsFactoryNumberUniqueAsync(int templateId, string factoryNumber, int? excludeDocumentId = null)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var query = context.Documents
                    .Where(d => d.DocumentTemplateId == templateId && d.FactoryNumber == factoryNumber);

                if (excludeDocumentId.HasValue)
                {
                    query = query.Where(d => d.Id != excludeDocumentId.Value);
                }

                return !await query.AnyAsync();
            }
        }

        public async Task<Document> CreateDocumentAsync(Document document, Dictionary<string, string> fieldValues)
        {
            _logger.LogInformation($"Создание документа для шаблона {document.DocumentTemplateId}, заводской номер: {document.FactoryNumber}");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                using (var transaction = await context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        context.Documents.Add(document);
                        await context.SaveChangesAsync();

                        var fields = await context.DocumentFields
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

                                context.DocumentValues.Add(documentValue);
                                _logger.LogDebug($"Добавлено значение для поля {field.FieldName}: {value}");
                            }
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation($"Документ успешно создан с ID: {document.Id}");
                        return document;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при создании документа");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task<bool> UpdateDocumentAsync(Document document, Dictionary<string, string> fieldValues)
        {
            _logger.LogInformation($"Обновление документа ID: {document.Id}");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                using (var transaction = await context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        context.Entry(document).State = EntityState.Modified;

                        // Удаляем существующие значения полей
                        var existingValues = await context.DocumentValues
                            .Where(v => v.DocumentId == document.Id)
                            .ToListAsync();

                        context.DocumentValues.RemoveRange(existingValues);
                        _logger.LogDebug($"Удалено {existingValues.Count} старых значений полей");

                        // Добавляем новые значения полей
                        var fields = await context.DocumentFields
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

                                context.DocumentValues.Add(documentValue);
                                _logger.LogDebug($"Добавлено новое значение для поля {field.FieldName}: {value}");
                            }
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation($"Документ ID: {document.Id} успешно обновлен");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Ошибка при обновлении документа ID: {document.Id}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task<bool> DeleteDocumentAsync(int id)
        {
            _logger.LogInformation($"Удаление документа ID: {id}");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var document = await context.Documents.FindAsync(id);

                if (document == null)
                {
                    _logger.LogWarning($"Документ ID: {id} не найден для удаления");
                    return false;
                }

                context.Documents.Remove(document);
                await context.SaveChangesAsync();

                _logger.LogInformation($"Документ ID: {id} успешно удален");
                return true;
            }
        }

        public async Task<Dictionary<string, string>> GetDocumentValuesAsync(int documentId)
        {
            _logger.LogInformation($"Получение значений полей для документа ID: {documentId}");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var values = await context.DocumentValues
                    .Include(v => v.DocumentField)
                    .Where(v => v.DocumentId == documentId)
                    .ToListAsync();

                var result = new Dictionary<string, string>();

                foreach (var value in values)
                {
                    result[value.DocumentField.FieldName] = value.Value;
                    _logger.LogDebug($"Получено значение для поля {value.DocumentField.FieldName}: {value.Value}");
                }

                _logger.LogInformation($"Получено {result.Count} значений полей для документа ID: {documentId}");
                return result;
            }
        }

        public async Task<bool> RelateDocumentsAsync(int parentDocumentId, int childDocumentId)
        {
            _logger.LogInformation($"Связывание документов: родительский ID: {parentDocumentId}, дочерний ID: {childDocumentId}");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var relation = new DocumentRelation
                {
                    ParentDocumentId = parentDocumentId,
                    ChildDocumentId = childDocumentId
                };

                context.DocumentRelations.Add(relation);

                try
                {
                    await context.SaveChangesAsync();
                    _logger.LogInformation($"Документы успешно связаны");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при связывании документов");
                    return false;
                }
            }
        }

        public async Task<IEnumerable<Document>> GetRelatedDocumentsAsync(int documentId)
        {
            _logger.LogInformation($"Получение связанных документов для ID: {documentId}");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var relations = await context.DocumentRelations
                    .Where(r => r.ParentDocumentId == documentId)
                    .Include(r => r.ChildDocument)
                        .ThenInclude(d => d.DocumentTemplate)
                    .ToListAsync();

                var relatedDocs = relations.Select(r => r.ChildDocument).ToList();
                _logger.LogInformation($"Найдено {relatedDocs.Count} связанных документов для ID: {documentId}");

                return relatedDocs;
            }
        }

        public async Task<bool> UpdateDocumentContentAsync(int documentId, byte[] content, string filePath = null)
        {
            _logger.LogInformation($"Обновление содержимого документа ID: {documentId}");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var document = await context.Documents.FindAsync(documentId);

                if (document == null)
                {
                    _logger.LogWarning($"Документ ID: {documentId} не найден для обновления содержимого");
                    return false;
                }

                document.DocumentContent = content;

                if (!string.IsNullOrEmpty(filePath))
                {
                    document.GeneratedFilePath = filePath;
                    _logger.LogDebug($"Обновлен путь к файлу: {filePath}");
                }

                await context.SaveChangesAsync();
                _logger.LogInformation($"Содержимое документа ID: {documentId} успешно обновлено");

                return true;
            }
        }
    }
}