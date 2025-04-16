using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DocumentManager.Infrastructure.Services
{
    /// <summary>
    /// Обработчик для бинарных файлов .doc с заменой плейсхолдеров
    /// </summary>
    public class DocBinaryTemplateHandler
    {
        private readonly ILogger _logger;

        public DocBinaryTemplateHandler(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Обрабатывает шаблон .doc файла с заданными значениями полей
        /// </summary>
        public bool ProcessTemplate(string templatePath, Dictionary<string, string> fieldValues, string outputPath)
        {
            try
            {
                _logger.LogInformation($"Обработка шаблона .doc: {templatePath}");

                // Регистрируем поддержку кодовых страниц Windows
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Используем кодировку Windows-1251 для русского языка
                var encoding = Encoding.GetEncoding(1251);

                // Читаем шаблон
                byte[] templateBytes = File.ReadAllBytes(templatePath);

                // Конвертируем в строку используя правильную кодировку
                string content = encoding.GetString(templateBytes);

                // Заменяем все плейсхолдеры формата {{ИмяПоля}}
                foreach (var field in fieldValues)
                {
                    string placeholder = $"{{{{{field.Key}}}}}";
                    string value = field.Value ?? string.Empty;

                    // Простая замена строки
                    content = content.Replace(placeholder, value);

                    _logger.LogDebug($"Замена '{placeholder}' на '{value}'");
                }

                // Конвертируем обратно в байты, используя ту же кодировку
                byte[] resultBytes = encoding.GetBytes(content);

                // Записываем в выходной файл
                File.WriteAllBytes(outputPath, resultBytes);

                _logger.LogInformation($"Шаблон обработан и сохранен в: {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обработке шаблона .doc: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Находит плейсхолдеры в шаблоне .doc
        /// </summary>
        public List<string> FindPlaceholders(string templatePath)
        {
            try
            {
                var placeholders = new List<string>();

                // Регистрируем поддержку кодовых страниц Windows
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Используем кодировку Windows-1251 для русского языка
                var encoding = Encoding.GetEncoding(1251);

                // Читаем шаблон
                byte[] templateBytes = File.ReadAllBytes(templatePath);

                // Конвертируем в строку используя правильную кодировку
                string content = encoding.GetString(templateBytes);

                // Ищем все вхождения {{...}}
                var regex = new Regex(@"\{\{([^}]+)\}\}");
                var matches = regex.Matches(content);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        placeholders.Add(match.Groups[1].Value);
                    }
                }

                return placeholders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при поиске плейсхолдеров в шаблоне .doc: {ex.Message}");
                return new List<string>();
            }
        }
    }
}