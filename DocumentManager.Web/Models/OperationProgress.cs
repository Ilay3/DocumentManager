using System;

namespace DocumentManager.Web.Models
{
    /// <summary>
    /// Модель прогресса операции
    /// </summary>
    public class OperationProgress
    {
        /// <summary>
        /// Уникальный идентификатор операции
        /// </summary>
        public string OperationId { get; set; }

        /// <summary>
        /// Время создания операции
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Время последнего обновления операции
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }

        /// <summary>
        /// Процент выполнения операции (от 0 до 100)
        /// </summary>
        public int PercentComplete { get; set; }

        /// <summary>
        /// Текущее сообщение о прогрессе
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Флаг завершения операции
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Флаг ошибки при выполнении операции
        /// </summary>
        public bool Error { get; set; }

        /// <summary>
        /// Результат операции (обычно URL для перенаправления)
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// Дополнительные данные операции (в формате JSON)
        /// </summary>
        public string AdditionalData { get; set; }
    }
}