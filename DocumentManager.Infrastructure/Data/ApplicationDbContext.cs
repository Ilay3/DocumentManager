// DocumentManager.Infrastructure/Data/ApplicationDbContext.cs
using DocumentManager.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using Document = DocumentManager.Core.Entities.Document;

namespace DocumentManager.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<DocumentTemplate> DocumentTemplates { get; set; }
        public DbSet<DocumentField> DocumentFields { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentValue> DocumentValues { get; set; }
        public DbSet<DocumentRelation> DocumentRelations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // DocumentTemplate
            modelBuilder.Entity<DocumentTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
                entity.Property(e => e.WordTemplatePath).IsRequired().HasMaxLength(255);
                entity.Property(e => e.JsonSchemaPath).IsRequired().HasMaxLength(255);

                // Индекс для ускорения поиска по коду
                entity.HasIndex(e => e.Code).IsUnique();
            });

            // DocumentField
            modelBuilder.Entity<DocumentField>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FieldName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.FieldLabel).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FieldType).IsRequired().HasMaxLength(50);

                // Связь с шаблоном документа
                entity.HasOne(e => e.DocumentTemplate)
                      .WithMany(e => e.Fields)
                      .HasForeignKey(e => e.DocumentTemplateId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Индекс для ускорения поиска полей по шаблону
                entity.HasIndex(e => new { e.DocumentTemplateId, e.FieldName }).IsUnique();
            });

            // Document
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FactoryNumber).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);

                // Связь с шаблоном документа
                entity.HasOne(e => e.DocumentTemplate)
                      .WithMany(e => e.Documents)
                      .HasForeignKey(e => e.DocumentTemplateId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Индекс для проверки уникальности заводского номера
                entity.HasIndex(e => new { e.DocumentTemplateId, e.FactoryNumber }).IsUnique();
            });

            // DocumentValue
            modelBuilder.Entity<DocumentValue>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Value).IsRequired();

                // Связь с документом
                entity.HasOne(e => e.Document)
                      .WithMany(e => e.Values)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Связь с полем документа
                entity.HasOne(e => e.DocumentField)
                      .WithMany()
                      .HasForeignKey(e => e.DocumentFieldId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Индекс для ускорения поиска значений по документу и полю
                entity.HasIndex(e => new { e.DocumentId, e.DocumentFieldId }).IsUnique();
            });

            // DocumentRelation
            modelBuilder.Entity<DocumentRelation>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Связь с родительским документом
                entity.HasOne(e => e.ParentDocument)
                      .WithMany(e => e.RelatedDocuments)
                      .HasForeignKey(e => e.ParentDocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Связь с дочерним документом
                entity.HasOne(e => e.ChildDocument)
                      .WithMany()
                      .HasForeignKey(e => e.ChildDocumentId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Индекс для ускорения поиска связей
                entity.HasIndex(e => new { e.ParentDocumentId, e.ChildDocumentId }).IsUnique();
            });
        }
    }
}