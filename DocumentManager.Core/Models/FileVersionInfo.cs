// DocumentManager.Core/Models/FileVersionInfo.cs
namespace DocumentManager.Core.Models
{
    /// <summary>
    /// Информация о версии файла шаблона
    /// </summary>
    public class FileVersionInfo
    {
        /// <summary>
        /// Полный путь к файлу
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Относительный путь от базовой директории
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Имя файла без расширения
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Номер версии, извлеченный из скобок
        /// </summary>
        public int? VersionNumber { get; set; }

        /// <summary>
        /// Время последнего изменения файла
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Размер файла
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Базовое имя файла без версии (для группировки)
        /// </summary>
        public string BaseFileName { get; set; }
    }
}