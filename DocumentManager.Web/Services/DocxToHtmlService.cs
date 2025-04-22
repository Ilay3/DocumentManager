using System;
using System.IO;
using System.Xml.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using DocumentFormat.OpenXml.Packaging;
using OpenXmlPowerTools;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentManager.Web.Services
{
    /// <summary>
    /// Сервис для конвертации DOCX-документов в HTML для печати через браузер
    /// </summary>
    public class DocxToHtmlService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<DocxToHtmlService> _logger;
        private readonly string _outputBasePath;

        public DocxToHtmlService(
            IMemoryCache cache,
            ILogger<DocxToHtmlService> logger,
            IConfiguration configuration)
        {
            _cache = cache;
            _logger = logger;
            _outputBasePath = configuration["OutputBasePath"] ?? Path.GetTempPath();
        }

        /// <summary>
        /// Конвертирует DOCX-документ в HTML для печати
        /// </summary>
        /// <param name="docxContent">Содержимое DOCX-файла</param>
        /// <param name="documentId">Идентификатор документа (для кэширования)</param>
        /// <param name="useCache">Использовать ли кэширование</param>
        /// <returns>HTML-контент для отображения и печати</returns>
        public string ConvertDocxToHtml(byte[] docxContent, string documentId, bool useCache = true)
        {
            if (docxContent == null) throw new ArgumentNullException(nameof(docxContent));
            string cacheKey = $"DocumentHtml_{documentId}";

            if (useCache && _cache.TryGetValue(cacheKey, out string cachedHtml))
            {
                _logger.LogInformation($"Получен HTML из кэша для документа {documentId}");
                return cachedHtml;
            }

            try
            {
                // Создаем расширяемый MemoryStream и записываем туда содержимое
                using var ms = new MemoryStream();
                ms.Write(docxContent, 0, docxContent.Length);
                ms.Position = 0;

                // Открываем документ в режиме чтения/записи для корректной работы PowerTools
                using var wordDoc = WordprocessingDocument.Open(ms, true);

                // Путь для возможного сохранения изображений
                string imageFolder = Path.Combine(_outputBasePath, "HtmlImages", documentId);
                Directory.CreateDirectory(imageFolder);

                var settings = new HtmlConverterSettings
                {
                    PageTitle = "Просмотр документа",
                    FabricateCssClasses = true,
                    CssClassPrefix = "docx-",
                    RestrictToSupportedLanguages = false,
                    RestrictToSupportedNumberingFormats = false,
                    ImageHandler = imageInfo =>
                    {
                        using var imgStream = new MemoryStream();
                        imageInfo.Bitmap.Save(imgStream, ImageFormat.Png);
                        string base64 = Convert.ToBase64String(imgStream.ToArray());
                        return new XElement("img",
                            new XAttribute("src", $"data:{imageInfo.ContentType};base64,{base64}"),
                            new XAttribute("alt", imageInfo.AltText ?? string.Empty)
                        );
                    }
                };

                // Конвертация в HTML
                XElement htmlElement = HtmlConverter.ConvertToHtml(wordDoc, settings);
                string rawHtml = htmlElement.ToString(SaveOptions.DisableFormatting);

                // Извлечение стилей
                string styleContent = string.Empty;
                int headStart = rawHtml.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
                int headEnd = rawHtml.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                if (headStart >= 0 && headEnd > headStart)
                {
                    string headInner = rawHtml.Substring(headStart + 6, headEnd - headStart - 6);
                    int styleStart = headInner.IndexOf("<style>", StringComparison.OrdinalIgnoreCase);
                    int styleEnd = headInner.IndexOf("</style>", StringComparison.OrdinalIgnoreCase);
                    if (styleStart >= 0 && styleEnd > styleStart)
                    {
                        styleContent = headInner.Substring(styleStart + 7, styleEnd - styleStart - 7);
                    }
                }

                // Извлечение тела документа
                string bodyContent = string.Empty;
                int bodyStart = rawHtml.IndexOf("<body>", StringComparison.OrdinalIgnoreCase);
                int bodyEnd = rawHtml.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                if (bodyStart >= 0 && bodyEnd > bodyStart)
                {
                    bodyContent = rawHtml.Substring(bodyStart + 6, bodyEnd - bodyStart - 6);
                }

                // Формируем итоговый HTML
                string finalHtml = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Просмотр документа</title>
    <style>
{styleContent}
        @media print {{
            body {{ margin: 0; padding: 0; font-size: 12pt; }}
            table {{ width: 100%; border-collapse: collapse; }}
            img {{ max-width: 100%; }}
        }}
    </style>
</head>
<body>
{bodyContent}
</body>
</html>";

                // Кэширование результата
                if (useCache)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
                        Size = finalHtml.Length
                    };
                    _cache.Set(cacheKey, finalHtml, cacheOptions);
                }

                return finalHtml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при конвертации DOCX в HTML для документа {documentId}");
                throw;
            }
        }

        /// <summary>
        /// Пытается конвертировать DOC в DOCX для последующей конвертации
        /// </summary>
        public bool TryConvertDoc(byte[] docContent, out byte[] docxContent)
        {
            docxContent = null;
            _logger.LogWarning("Конвертация DOC в DOCX пока не реализована");
            return false;
        }
    }
}