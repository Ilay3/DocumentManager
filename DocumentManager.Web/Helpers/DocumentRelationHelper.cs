using DocumentManager.Core.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DocumentManager.Web.Helpers
{
    public static class DocumentRelationHelper
    {
        /// <summary>
        /// Находит связанные шаблоны документов для указанного шаблона
        /// </summary>
        public static IEnumerable<DocumentTemplate> FindRelatedTemplates(
            DocumentTemplate template,
            IEnumerable<DocumentTemplate> allTemplates)
        {
            if (template == null || allTemplates == null)
                return Enumerable.Empty<DocumentTemplate>();

            // Получаем базовую папку комплекта (например "ЭСУВТ.01-06-00 (Депо)")
            string kitFolder = GetKitFolder(template.WordTemplatePath);
            if (string.IsNullOrEmpty(kitFolder))
                return Enumerable.Empty<DocumentTemplate>();

            // Определяем типы документов, которые могут быть связаны
            var allowedTypes = template.Type == "Passport"
                ? new[] { "PackingList", "PackingInventory" }
                : new[] { "Passport", "PackingList", "PackingInventory" };

            return allTemplates
                .Where(t => t != null &&
                       t.IsActive &&
                       t.Id != template.Id &&
                       allowedTypes.Contains(t.Type) &&
                       GetKitFolder(t.WordTemplatePath) == kitFolder)
                .ToList();
        }

        /// <summary>
        /// Извлекает название папки комплекта из пути
        /// </summary>
        private static string GetKitFolder(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            // Нормализуем путь и разбиваем на части
            var parts = filePath.Replace('\\', '/')
                               .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Папка комплекта всегда находится на предпоследнем уровне
            // Например: "Passport/ЭСУВТ.01-06-00 (Депо)/файл.docx"
            if (parts.Length >= 2)
            {
                // Удаляем возможные пробелы в конце скобок
                return parts[^2].TrimEnd();
            }

            return null;
        }

        /// <summary>
        /// Проверяет, относятся ли документы к одному комплекту
        /// </summary>
        public static bool AreTemplatesRelated(DocumentTemplate template1, DocumentTemplate template2)
        {
            if (template1 == null || template2 == null)
                return false;

            string folder1 = GetKitFolder(template1.WordTemplatePath);
            string folder2 = GetKitFolder(template2.WordTemplatePath);

            return folder1 != null && folder1 == folder2;
        }

        /// <summary>
        /// Группирует документы по комплектам
        /// </summary>
        public static Dictionary<string, List<DocumentTemplate>> GroupTemplatesByKit(
            IEnumerable<DocumentTemplate> templates)
        {
            var result = new Dictionary<string, List<DocumentTemplate>>();

            foreach (var template in templates.Where(t => t != null && t.IsActive))
            {
                string kitFolder = GetKitFolder(template.WordTemplatePath);
                if (string.IsNullOrEmpty(kitFolder))
                    continue;

                if (!result.ContainsKey(kitFolder))
                {
                    result[kitFolder] = new List<DocumentTemplate>();
                }

                result[kitFolder].Add(template);
            }

            return result;
        }
    }
}