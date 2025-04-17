namespace DocumentManager.Web.Models
{
    /// <summary>
    /// Модель представления для отображения прогресса генерации документа
    /// </summary>
    public class GenerateProgressViewModel
    {
        /// <summary>
        /// Идентификатор документа
        /// </summary>
        public int DocumentId { get; set; }

        /// <summary>
        /// Идентификатор операции генерации
        /// </summary>
        public string OperationId { get; set; }
    }

}
