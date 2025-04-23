// DocumentManager.Web/Services/ProgressService.cs
using System.Collections.Concurrent;

namespace DocumentManager.Web.Services
{
    /// <summary>
    /// Сервис для отслеживания прогресса длительных операций
    /// </summary>
    public class ProgressService
    {
        private readonly ConcurrentDictionary<string, ProgressInfo> _progressStorage = new();

        /// <summary>
        /// Создать новую операцию отслеживания прогресса
        /// </summary>
        /// <param name="operationId">Идентификатор операции (если null, будет сгенерирован)</param>
        /// <returns>Идентификатор операции</returns>
        public string CreateOperation(string operationId = null)
        {
            operationId ??= Guid.NewGuid().ToString();

            var progressInfo = new ProgressInfo
            {
                OperationId = operationId,
                StartTime = DateTime.UtcNow,
                Progress = 0,
                Status = "Инициализация...",
                IsCompleted = false
            };

            _progressStorage[operationId] = progressInfo;
            return operationId;
        }

        /// <summary>
        /// Обновить прогресс операции
        /// </summary>
        public void UpdateProgress(string operationId, int progress, string status = null)
        {
            if (!_progressStorage.TryGetValue(operationId, out var progressInfo))
            {
                throw new ArgumentException($"Операция {operationId} не найдена");
            }

            progressInfo.Progress = progress;

            if (status != null)
            {
                progressInfo.Status = status;
            }

            progressInfo.LastUpdateTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Завершить операцию
        /// </summary>
        public void CompleteOperation(string operationId, string status = "Операция завершена")
        {
            if (!_progressStorage.TryGetValue(operationId, out var progressInfo))
            {
                throw new ArgumentException($"Операция {operationId} не найдена");
            }

            progressInfo.Progress = 100;
            progressInfo.Status = status;
            progressInfo.IsCompleted = true;
            progressInfo.CompletionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Завершить операцию с ошибкой
        /// </summary>
        public void CompleteOperationWithError(string operationId, string errorMessage)
        {
            if (!_progressStorage.TryGetValue(operationId, out var progressInfo))
            {
                throw new ArgumentException($"Операция {operationId} не найдена");
            }

            progressInfo.Status = $"Ошибка: {errorMessage}";
            progressInfo.IsCompleted = true;
            progressInfo.IsError = true;
            progressInfo.CompletionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Получить информацию о прогрессе операции
        /// </summary>
        public ProgressInfo GetProgress(string operationId)
        {
            if (!_progressStorage.TryGetValue(operationId, out var progressInfo))
            {
                throw new ArgumentException($"Операция {operationId} не найдена");
            }

            return progressInfo;
        }

        /// <summary>
        /// Удалить операцию из хранилища (для завершенных операций)
        /// </summary>
        public void RemoveOperation(string operationId)
        {
            _progressStorage.TryRemove(operationId, out _);
        }

        /// <summary>
        /// Очистить устаревшие операции
        /// </summary>
        public void CleanupOldOperations(TimeSpan maxAge)
        {
            var cutoffTime = DateTime.UtcNow - maxAge;

            foreach (var key in _progressStorage.Keys)
            {
                if (_progressStorage.TryGetValue(key, out var progressInfo))
                {
                    var lastActivityTime = progressInfo.CompletionTime ?? progressInfo.LastUpdateTime ?? progressInfo.StartTime;

                    if (lastActivityTime < cutoffTime)
                    {
                        _progressStorage.TryRemove(key, out _);
                    }
                }
            }
        }


    }



    /// <summary>
    /// Информация о прогрессе операции
    /// </summary>
    public class ProgressInfo
    {
        /// <summary>
        /// Идентификатор операции
        /// </summary>
        public string OperationId { get; set; }

        /// <summary>
        /// Процент выполнения (0-100)
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// Текущий статус операции
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Флаг завершения операции
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Флаг ошибки
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// Время начала операции
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Время последнего обновления прогресса
        /// </summary>
        public DateTime? LastUpdateTime { get; set; }

        /// <summary>
        /// Время завершения операции
        /// </summary>
        public DateTime? CompletionTime { get; set; }

        /// <summary>
        /// Результат операции (например, URL для скачивания файла)
        /// </summary>
        public string Result { get; set; }
    }
}