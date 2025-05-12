// DocumentManager.Core/Models/TemplateVersionUpdate.cs
namespace DocumentManager.Core.Models
{
    /// <summary>
    /// Информация об обновлении шаблона
    /// </summary>
    public class TemplateVersionUpdate
    {
        /// <summary>
        /// ID шаблона в базе данных
        /// </summary>
        public int TemplateId { get; set; }

        /// <summary>
        /// Код шаблона
        /// </summary>
        public string TemplateCode { get; set; }

        /// <summary>
        /// Старый путь к файлу
        /// </summary>
        public string OldPath { get; set; }

        /// <summary>
        /// Новый путь к файлу
        /// </summary>
        public string NewPath { get; set; }

        /// <summary>
        /// Старая версия
        /// </summary>
        public int? OldVersion { get; set; }

        /// <summary>
        /// Новая версия
        /// </summary>
        public int? NewVersion { get; set; }

        /// <summary>
        /// Время обнаружения изменения
        /// </summary>
        public DateTime DetectedAt { get; set; }

        /// <summary>
        /// Применено ли обновление
        /// </summary>
        public bool IsApplied { get; set; }
    }
}