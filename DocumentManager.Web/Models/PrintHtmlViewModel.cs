namespace DocumentManager.Web.Models
{
    /// <summary>
    /// Модель представления для отображения HTML-версии документа для печати
    /// </summary>
    public class PrintHtmlViewModel
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
        /// HTML-содержимое документа
        /// </summary>
        public string HtmlContent { get; set; }
    }
}