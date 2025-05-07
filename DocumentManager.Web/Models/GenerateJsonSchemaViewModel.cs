using DocumentManager.Infrastructure.Services;
using System.Collections.Generic;

namespace DocumentManager.Web.Models
{
    /// <summary>
    /// Модель представления для страницы генерации JSON-схемы
    /// </summary>
    public class GenerateJsonSchemaViewModel
    {
        /// <summary>
        /// Список шаблонов Word
        /// </summary>
        public List<TemplateFileInfo> WordTemplates { get; set; } = new List<TemplateFileInfo>();

        /// <summary>
        /// Выбранный путь к шаблону
        /// </summary>
        public string SelectedWordTemplatePath { get; set; }

        /// <summary>
        /// Список папок с шаблонами для пакетной генерации
        /// </summary>
        public List<string> TemplateFolders { get; set; } = new List<string>();

        /// <summary>
        /// Выбранная папка
        /// </summary>
        public string SelectedFolder { get; set; }
    }

    /// <summary>
    /// Модель представления для результата генерации JSON-схемы
    /// </summary>
    public class JsonGenerationResultViewModel
    {
        /// <summary>
        /// Успешно ли завершилась генерация
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Полный путь к сгенерированному JSON-файлу
        /// </summary>
        public string JsonFilePath { get; set; }

        /// <summary>
        /// Относительный путь к JSON-файлу
        /// </summary>
        public string RelativeJsonPath { get; set; }

        /// <summary>
        /// Количество найденных плейсхолдеров
        /// </summary>
        public int PlaceholdersCount { get; set; }

        /// <summary>
        /// Список найденных плейсхолдеров
        /// </summary>
        public List<string> Placeholders { get; set; } = new List<string>();

        /// <summary>
        /// Сообщение об ошибке (если генерация не удалась)
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Модель представления для результата пакетной генерации JSON-схем
    /// </summary>
    public class BatchJsonGenerationResultViewModel
    {
        /// <summary>
        /// Путь к папке с шаблонами
        /// </summary>
        public string FolderPath { get; set; }

        /// <summary>
        /// Общее количество файлов
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Количество успешно сгенерированных файлов
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Результаты генерации для каждого файла
        /// </summary>
        public List<JsonGenerationResult> Results { get; set; } = new List<JsonGenerationResult>();
    }
}