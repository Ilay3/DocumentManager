using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace DocumentManager.Infrastructure.Services
{
    /// <summary>
    /// Улучшенный обработчик для бинарных файлов .doc с поддержкой разных форматов плейсхолдеров
    /// </summary>
    public class DocBinaryTemplateHandler
    {
        private readonly ILogger _logger;
        private readonly List<string> _placeholderFormats = new List<string>
        {
            @"\{\{([^}]+)\}\}", // Формат {{FieldName}}
            @"<<([^>]+)>>",     // Формат <<FieldName>>
            @"\[([^\]]+)\]",    // Формат [FieldName]
            @"\$([a-zA-Z0-9_]+)" // Формат $FieldName
        };

        public DocBinaryTemplateHandler(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Обрабатывает шаблон .doc файла с заданными значениями полей
        /// </summary>
        public byte[] ProcessTemplate(string templatePath, Dictionary<string, string> fieldValues)
        {
            try
            {
                _logger.LogInformation($"Обработка шаблона .doc: {templatePath}");

                // Регистрируем поддержку кодовых страниц Windows
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Используем разные кодировки для максимального охвата
                var encodings = new List<Encoding>
                {
                    Encoding.GetEncoding(1251),  // Windows-1251 для русского языка
                    Encoding.ASCII,              // ASCII для базовых символов
                    Encoding.UTF8,               // UTF-8 для универсальной поддержки
                    Encoding.Unicode,            // Unicode (UTF-16)
                    Encoding.GetEncoding(866)    // DOS кириллица
                };

                // Читаем шаблон
                byte[] templateBytes = File.ReadAllBytes(templatePath);

                string content = null;
                Dictionary<string, string> foundPlaceholders = new Dictionary<string, string>();
                List<string> allPlaceholders = new List<string>();

                // Пробуем разные кодировки
                foreach (var encoding in encodings)
                {
                    try
                    {
                        // Конвертируем в строку с текущей кодировкой
                        string tempContent = encoding.GetString(templateBytes);

                        // Ищем плейсхолдеры во всех поддерживаемых форматах
                        Dictionary<string, string> tempPlaceholders = new Dictionary<string, string>();

                        foreach (var format in _placeholderFormats)
                        {
                            var regex = new Regex(format);
                            var matches = regex.Matches(tempContent);

                            foreach (Match match in matches)
                            {
                                if (match.Groups.Count > 1)
                                {
                                    string placeholder = match.Groups[1].Value;
                                    string fullMatch = match.Value;

                                    // Добавляем в словарь плейсхолдеров
                                    if (!tempPlaceholders.ContainsKey(placeholder))
                                    {
                                        tempPlaceholders[placeholder] = fullMatch;
                                    }
                                }
                            }
                        }

                        // Если нашли плейсхолдеры с этой кодировкой, запоминаем результаты
                        if (tempPlaceholders.Count > 0 && (foundPlaceholders.Count == 0 || tempPlaceholders.Count > foundPlaceholders.Count))
                        {
                            content = tempContent;
                            foundPlaceholders = tempPlaceholders;
                            _logger.LogInformation($"Найдены плейсхолдеры с кодировкой {encoding.EncodingName}. Всего: {tempPlaceholders.Count}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Ошибка при чтении с кодировкой {encoding.EncodingName}: {ex.Message}");
                    }
                }

                // Если не нашли плейсхолдеров, используем Windows-1251
                if (content == null)
                {
                    content = Encoding.GetEncoding(1251).GetString(templateBytes);
                    _logger.LogWarning("Не удалось найти плейсхолдеры ни с одной кодировкой. Используем Windows-1251 по умолчанию.");
                }

                // Логируем найденные плейсхолдеры
                if (foundPlaceholders.Count > 0)
                {
                    _logger.LogInformation($"Найдено {foundPlaceholders.Count} плейсхолдеров в шаблоне: {string.Join(", ", foundPlaceholders.Keys)}");

                    foreach (var placeholder in foundPlaceholders)
                    {
                        _logger.LogDebug($"Плейсхолдер: '{placeholder.Key}', полное совпадение: '{placeholder.Value}'");
                    }

                    // Сравниваем найденные плейсхолдеры с переданными значениями
                    foreach (var placeholder in foundPlaceholders.Keys)
                    {
                        string key = placeholder;
                        // Пробуем найти подходящее значение, даже если имя немного отличается
                        var matchingKey = fieldValues.Keys.FirstOrDefault(k =>
                            k.Equals(placeholder, StringComparison.OrdinalIgnoreCase) ||
                            k.Replace("_", "").Equals(placeholder.Replace("_", ""), StringComparison.OrdinalIgnoreCase));

                        if (matchingKey != null)
                        {
                            _logger.LogInformation($"Плейсхолдер '{placeholder}' будет заменен на значение: {fieldValues[matchingKey]}");
                        }
                        else
                        {
                            _logger.LogWarning($"Плейсхолдер '{placeholder}' найден в шаблоне, но значение для него не предоставлено");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Плейсхолдеры не найдены в шаблоне!");

                    // Экстренная попытка - проверим, содержит ли документ вообще какой-то текст
                    _logger.LogInformation("Пример содержимого документа (первые 500 символов):");
                    _logger.LogInformation(content.Length > 500 ? content.Substring(0, 500) : content);
                }

                // Проверяем, какие значения не используются в шаблоне
                foreach (var field in fieldValues)
                {
                    bool found = false;
                    foreach (var placeholder in foundPlaceholders.Keys)
                    {
                        if (field.Key.Equals(placeholder, StringComparison.OrdinalIgnoreCase) ||
                            field.Key.Replace("_", "").Equals(placeholder.Replace("_", ""), StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        _logger.LogWarning($"Значение '{field.Key}' предоставлено, но соответствующий плейсхолдер не найден в шаблоне");
                    }
                }

                // Заменяем все плейсхолдеры в разных форматах
                int replacementCount = 0;

                // Сначала готовим список замен
                var replacements = new List<(string Original, string Replacement)>();

                foreach (var field in fieldValues)
                {
                    foreach (var format in _placeholderFormats)
                    {
                        string pattern = format.Replace("([^}]+)", Regex.Escape(field.Key))
                                               .Replace("([^>]+)", Regex.Escape(field.Key))
                                               .Replace("([^\\]]+)", Regex.Escape(field.Key))
                                               .Replace("([a-zA-Z0-9_]+)", Regex.Escape(field.Key));

                        var matches = Regex.Matches(content, pattern);
                        foreach (Match match in matches)
                        {
                            replacements.Add((match.Value, field.Value ?? string.Empty));
                            _logger.LogDebug($"Запланирована замена '{match.Value}' на '{field.Value}'");
                        }
                    }

                    // Также проверяем плейсхолдеры, которые мы уже нашли
                    foreach (var placeholder in foundPlaceholders)
                    {
                        if (field.Key.Equals(placeholder.Key, StringComparison.OrdinalIgnoreCase) ||
                            field.Key.Replace("_", "").Equals(placeholder.Key.Replace("_", ""), StringComparison.OrdinalIgnoreCase))
                        {
                            replacements.Add((placeholder.Value, field.Value ?? string.Empty));
                            _logger.LogDebug($"Запланирована замена '{placeholder.Value}' на '{field.Value}'");
                        }
                    }
                }

                // Затем делаем замены, начиная с самых длинных плейсхолдеров
                foreach (var replacement in replacements.OrderByDescending(r => r.Original.Length))
                {
                    string originalContent = content;
                    content = content.Replace(replacement.Original, replacement.Replacement);

                    // Проверяем, произошла ли замена
                    if (originalContent != content)
                    {
                        replacementCount++;
                        _logger.LogInformation($"Замена '{replacement.Original}' на '{replacement.Replacement}' - успешно");
                    }
                    else
                    {
                        _logger.LogWarning($"Не удалось заменить '{replacement.Original}' - строка не найдена");
                    }
                }

                _logger.LogInformation($"Выполнено {replacementCount} замен плейсхолдеров");

                // В бинарных файлах .doc могут быть специальные символы и структуры - пробуем улучшенный метод
                // Это обходной метод для работы с двоичным форматом .doc
                if (replacementCount == 0)
                {
                    _logger.LogWarning("Стандартные замены не сработали. Пробуем прямую бинарную замену...");

                    var result = templateBytes;
                    var encoder = Encoding.GetEncoding(1251);

                    foreach (var field in fieldValues)
                    {
                        foreach (var format in new[] { "{{" + field.Key + "}}", "<<" + field.Key + ">>", "[" + field.Key + "]", "$" + field.Key })
                        {
                            try
                            {
                                byte[] searchBytes = encoder.GetBytes(format);
                                byte[] replaceBytes = encoder.GetBytes(field.Value ?? string.Empty);

                                if (searchBytes.Length > 0)
                                {
                                    result = ReplaceByteSequence(result, searchBytes, replaceBytes);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Ошибка при бинарной замене '{format}': {ex.Message}");
                            }
                        }
                    }

                    _logger.LogInformation("Бинарная замена завершена.");
                    return result;
                }

                // Конвертируем обратно в байты, используя ту же кодировку
                byte[] resultBytes = Encoding.GetEncoding(1251).GetBytes(content);

                _logger.LogInformation($"Шаблон обработан успешно, размер результата: {resultBytes.Length} байт");
                return resultBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обработке шаблона .doc: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Заменяет последовательность байтов в бинарном файле
        /// </summary>
        private byte[] ReplaceByteSequence(byte[] source, byte[] search, byte[] replace)
        {
            List<byte> result = new List<byte>();
            int sourceIndex = 0;

            while (sourceIndex < source.Length)
            {
                bool found = true;

                // Проверяем, совпадает ли последовательность в данной позиции
                if (sourceIndex <= source.Length - search.Length)
                {
                    for (int i = 0; i < search.Length; i++)
                    {
                        if (source[sourceIndex + i] != search[i])
                        {
                            found = false;
                            break;
                        }
                    }
                }
                else
                {
                    found = false;
                }

                // Если нашли совпадение, добавляем замену
                if (found)
                {
                    result.AddRange(replace);
                    sourceIndex += search.Length;
                    _logger.LogDebug($"Найдена и заменена бинарная последовательность");
                }
                else
                {
                    // Иначе копируем текущий байт
                    result.Add(source[sourceIndex]);
                    sourceIndex++;
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Находит плейсхолдеры в шаблоне .doc
        /// </summary>
        public List<string> FindPlaceholders(string templatePath)
        {
            try
            {
                // Регистрируем поддержку кодовых страниц Windows
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Используем разные кодировки для максимального охвата
                var encodings = new List<Encoding>
                {
                    Encoding.GetEncoding(1251),  // Windows-1251 для русского языка
                    Encoding.ASCII,              // ASCII для базовых символов
                    Encoding.UTF8,               // UTF-8 для универсальной поддержки
                    Encoding.Unicode,            // Unicode (UTF-16)
                    Encoding.GetEncoding(866)    // DOS кириллица
                };

                // Читаем шаблон
                byte[] templateBytes = File.ReadAllBytes(templatePath);

                List<string> result = new List<string>();
                string bestContent = null;

                // Пробуем разные кодировки
                foreach (var encoding in encodings)
                {
                    try
                    {
                        // Конвертируем в строку с текущей кодировкой
                        string content = encoding.GetString(templateBytes);
                        List<string> placeholders = new List<string>();

                        // Ищем плейсхолдеры во всех поддерживаемых форматах
                        foreach (var format in _placeholderFormats)
                        {
                            var regex = new Regex(format);
                            var matches = regex.Matches(content);

                            foreach (Match match in matches)
                            {
                                if (match.Groups.Count > 1)
                                {
                                    placeholders.Add(match.Groups[1].Value);
                                }
                            }
                        }

                        // Если нашли больше плейсхолдеров с этой кодировкой, обновляем результат
                        if (placeholders.Count > result.Count)
                        {
                            result = placeholders;
                            bestContent = content;
                            _logger.LogInformation($"Найдено {placeholders.Count} плейсхолдеров с кодировкой {encoding.EncodingName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Ошибка при чтении с кодировкой {encoding.EncodingName}: {ex.Message}");
                    }
                }

                if (result.Count == 0 && bestContent != null)
                {
                    // Экстренная попытка - проверим, содержит ли документ вообще какой-то текст
                    _logger.LogInformation("Плейсхолдеры не найдены. Пример содержимого документа (первые 500 символов):");
                    _logger.LogInformation(bestContent.Length > 500 ? bestContent.Substring(0, 500) : bestContent);
                }

                return result.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при поиске плейсхолдеров в шаблоне .doc: {ex.Message}");
                return new List<string>();
            }
        }
    }
}