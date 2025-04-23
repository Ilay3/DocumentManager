using System;

namespace DocumentManager.Web.Models
{
    /// <summary>
    /// Модель представления для отображения PDF-версии документа для печати
    /// </summary>
    public class PrintPdfViewModel
    {
        /// <summary>
        /// Идентификатор документа
        /// </summary>
        public int DocumentId { get; set; }

        /// <summary>
        /// Наименование документа
        /// </summary>
        public string DocumentName { get; set; }

        /// <summary>
        /// Заводской номер
        /// </summary>
        public string FactoryNumber { get; set; }

        /// <summary>
        /// Дата создания
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Создатель документа
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// Путь к PDF-файлу
        /// </summary>
        public string PdfFilePath { get; set; }

        /// <summary>
        /// URL для скачивания PDF-файла
        /// </summary>
        public string PdfDownloadUrl { get; set; }
    }
}