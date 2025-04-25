// DocumentManager.Web/Helpers/DocumentRelationHelper.cs
using DocumentManager.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocumentManager.Web.Helpers
{
    /// <summary>
    /// Вспомогательный класс для определения связей между документами
    /// </summary>
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

            // Получаем очищенный код (без текста после пробела и без скобок)
            string cleanCode = CleanupCode(template.Code);

            if (template.Type == "Passport")
            {
                // Для паспорта ищем упаковочные листы, в которых есть ссылка на него
                var result = allTemplates.Where(t =>
                    t.Id != template.Id &&
                    t.IsActive &&
                    (t.Type == "PackingList" || t.Type == "PackingInventory") &&
                    IsPackingListRelatedToPassport(t, cleanCode));

                return result;
            }
            else if (template.Type == "PackingList")
            {
                // Для упаковочного листа находим связанный код паспорта
                string passportCode = FindReferencedPassportCode(template);

                if (!string.IsNullOrEmpty(passportCode))
                {
                    // Находим другие упаковочные листы, ссылающиеся на тот же паспорт
                    return allTemplates.Where(t =>
                        t.Id != template.Id &&
                        t.IsActive &&
                        (t.Type == "PackingList" || t.Type == "PackingInventory") &&
                        IsPackingListRelatedToPassport(t, passportCode));
                }
                else
                {
                    // Если не нашли ссылку на паспорт, ищем по совпадению базового кода
                    string baseCode = ExtractBaseCode(cleanCode);

                    return allTemplates.Where(t =>
                        t.Id != template.Id &&
                        t.IsActive &&
                        (t.Type == "PackingList" || t.Type == "PackingInventory") &&
                        ExtractBaseCode(CleanupCode(t.Code)) == baseCode);
                }
            }

            return Enumerable.Empty<DocumentTemplate>();
        }

        /// <summary>
        /// Проверяет, связан ли упаковочный лист с паспортом
        /// </summary>
        private static bool IsPackingListRelatedToPassport(DocumentTemplate packingList, string passportCode)
        {
            // Полный текст для поиска ссылок
            string fullText = packingList.Code + " " + packingList.Name;

            // 1. Ищем прямое упоминание кода паспорта в упаковочном листе
            if (fullText.Contains(passportCode))
            {
                return true;
            }

            // 2. Ищем альтернативную запись кода (с дефисами вместо точек и наоборот)
            string alternativeCode = passportCode.Replace(".", "-");
            if (passportCode.Contains(".") && fullText.Contains(alternativeCode))
            {
                return true;
            }

            alternativeCode = passportCode.Replace("-", ".");
            if (passportCode.Contains("-") && fullText.Contains(alternativeCode))
            {
                return true;
            }

            // 3. Извлекаем базовый код (часть до третьего разделителя)
            string basePassportCode = ExtractBaseCode(passportCode);

            // Поиск шаблона, который может быть ссылкой на паспорт
            // Например: ЭРЧМ30Т3, ЭСУВТ.01 и т.д.
            var codePatterns = FindPossibleCodePatterns(fullText);

            foreach (var pattern in codePatterns)
            {
                // Проверяем, содержит ли базовый код паспорта эту последовательность
                // или наоборот
                if (basePassportCode.Contains(pattern) || pattern.Contains(basePassportCode))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Находит возможные фрагменты кодов в тексте
        /// </summary>
        private static List<string> FindPossibleCodePatterns(string text)
        {
            var patterns = new List<string>();

            // Ищем фрагменты вида "ЭСУВТ.01", "ЭРЧМ30Т3" и т.д. 
            // Буквы кириллицы, затем возможны цифры, точки, дефисы, латинские буквы T
            var matches = Regex.Matches(text, @"[А-Я]{1,6}[0-9А-Я\.T-]{1,20}");

            foreach (Match match in matches)
            {
                string pattern = match.Value;

                // Фильтруем слишком короткие паттерны и общие слова
                if (pattern.Length >= 5 && !IsCommonWord(pattern))
                {
                    patterns.Add(pattern);
                }
            }

            return patterns;
        }

        /// <summary>
        /// Проверяет, является ли строка общим словом (не кодом)
        /// </summary>
        private static bool IsCommonWord(string text)
        {
            // Список общих слов, которые могут встречаться, но не являются кодами
            var commonWords = new[] { "ПАСПОРТ", "УПАКОВОЧНЫЙ", "ЛИСТ", "СИСТЕМА" };

            return commonWords.Contains(text);
        }

        /// <summary>
        /// Находит код паспорта, на который ссылается упаковочный лист
        /// </summary>
        private static string FindReferencedPassportCode(DocumentTemplate packingList)
        {
            string fullText = packingList.Code + " " + packingList.Name;

            // Извлекаем базовый код упаковочного листа
            string baseCode = ExtractBaseCode(CleanupCode(packingList.Code));

            // Ищем все возможные ссылки на коды в тексте
            var codePatterns = FindPossibleCodePatterns(fullText);

            // Отфильтровываем те, которые не похожи на базовый код упаковочного листа
            // (скорее всего, они относятся к ссылке на паспорт)
            return codePatterns
                .Where(p => !baseCode.Contains(p) && !p.Contains(baseCode))
                .FirstOrDefault() ?? string.Empty;
        }

        /// <summary>
        /// Извлекает базовую часть кода (до третьего разделителя)
        /// </summary>
        private static string ExtractBaseCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return string.Empty;

            // Разделяем код по точкам и дефисам
            var separators = new[] { '.', '-' };
            string[] parts = code.Split(separators);

            // Если в коде есть серия с буквой T (например, ЭРЧМ30Т3),
            // берем ее целиком как базовый код
            if (parts.Length > 0 && parts[0].Contains("Т"))
            {
                return parts[0];
            }

            // Если есть хотя бы 2 части, берем первые две
            if (parts.Length >= 2)
            {
                return parts[0] + "." + parts[1];
            }

            return code;
        }

        /// <summary>
        /// Очищает код от скобок и текста после пробела
        /// </summary>
        private static string CleanupCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return string.Empty;

            // Обрезаем всё после первого пробела
            if (code.Contains(" "))
            {
                code = code.Substring(0, code.IndexOf(" "));
            }

            // Удаляем скобки и их содержимое
            code = Regex.Replace(code, @"\([^)]*\)", "");

            return code;
        }
    }
}