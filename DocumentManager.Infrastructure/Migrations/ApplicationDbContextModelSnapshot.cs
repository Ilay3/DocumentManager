﻿// <auto-generated />
using System;
using DocumentManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocumentManager.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("DocumentManager.Core.Entities.Document", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("CreatedBy")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<int>("DocumentTemplateId")
                        .HasColumnType("integer");

                    b.Property<string>("FactoryNumber")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("GeneratedFilePath")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("DocumentTemplateId", "FactoryNumber")
                        .IsUnique();

                    b.ToTable("Documents");
                });

            modelBuilder.Entity("DocumentManager.Core.Entities.DocumentField", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Condition")
                        .HasColumnType("text");

                    b.Property<string>("DefaultValue")
                        .HasColumnType("text");

                    b.Property<int>("DocumentTemplateId")
                        .HasColumnType("integer");

                    b.Property<string>("FieldLabel")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("FieldName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("FieldType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("IsRequired")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsUnique")
                        .HasColumnType("boolean");

                    b.Property<string>("Options")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("DocumentTemplateId", "FieldName")
                        .IsUnique();

                    b.ToTable("DocumentFields");
                });

            modelBuilder.Entity("DocumentManager.Core.Entities.DocumentRelation", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("ChildDocumentId")
                        .HasColumnType("integer");

                    b.Property<int>("ParentDocumentId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("ChildDocumentId");

                    b.HasIndex("ParentDocumentId", "ChildDocumentId")
                        .IsUnique();

                    b.ToTable("DocumentRelations");
                });

            modelBuilder.Entity("DocumentManager.Core.Entities.DocumentTemplate", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("JsonSchemaPath")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("WordTemplatePath")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.HasKey("Id");

                    b.HasIndex("Code")
                        .IsUnique();

                    b.ToTable("DocumentTemplates");
                });

            modelBuilder.Entity("DocumentManager.Core.Entities.DocumentValue", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("DocumentFieldId")
                        .HasColumnType("integer");

                    b.Property<int>("DocumentId")
                        .HasColumnType("integer");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("DocumentFieldId");

                    b.HasIndex("DocumentId", "DocumentFieldId")
                        .IsUnique();

                    b.ToTable("DocumentValues");
                });

            modelBuilder.Entity("DocumentManager.Core.Entities.Document", b =>
                {
                    b.HasOne("DocumentManager.Core.Entities.DocumentTemplate", "DocumentTemplate")
                        .WithMany("Documents")
                        .HasForeignKey("DocumentTemplateId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("DocumentTemplate");
                });

            modelBuilder.Entity("DocumentManager.Core.Entities.DocumentField", b =>
                {
                    b.HasOne("DocumentManager.Core.Entities.DocumentTemplate", "DocumentTemplate")
                        .WithMany("Fields")
                        .HasForeignKey("DocumentTemplateId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DocumentTemplate");
                });

            modelBuilder.Entity("DocumentManager.Core.Entities.DocumentRelation", b =>
                {
                    b.HasOne("DocumentManager.Core.Entities.Document", "ChildDocument")
                        .WithMany()
                        .HasForeignKey("ChildDocumentId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("DocumentManager.Core.Entities.Document", "ParentDocument")
                        .WithMany("RelatedDocuments")
                        .HasForeignKey("ParentDocumentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ChildDocument");

                    b.Navigation("ParentDocument");
                });

            modelBuilder.Entity("DocumentManager.Core.Entities.DocumentValue", b =>
                {
                    b.HasOne("DocumentManager.Core.Entities.DocumentField", "DocumentField")
                        .WithMany()
                        .HasForeignKey("DocumentFieldId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("DocumentManager.Core.Entities.Document", "Document")
                        .WithMany("Values")
                        .HasForeignKey("DocumentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Document");

                    b.Navigation("DocumentField");
                });

            modelBuilder.Entity("DocumentManager.Core.Entities.Document", b =>
                {
                    b.Navigation("RelatedDocuments");

                    b.Navigation("Values");
                });

            modelBuilder.Entity("DocumentManager.Core.Entities.DocumentTemplate", b =>
                {
                    b.Navigation("Documents");

                    b.Navigation("Fields");
                });
#pragma warning restore 612, 618
        }
    }
}
