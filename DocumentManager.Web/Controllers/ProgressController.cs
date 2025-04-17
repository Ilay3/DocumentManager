// DocumentManager.Web/Controllers/ProgressController.cs
using DocumentManager.Web.Models;
using DocumentManager.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManager.Web.Controllers
{
    /// <summary>
    /// Контроллер для отслеживания прогресса операций
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ProgressController : ControllerBase
    {
        private readonly ProgressService _progressService;
        private readonly ILogger<ProgressController> _logger;

        public ProgressController(
            ProgressService progressService,
            ILogger<ProgressController> logger)
        {
            _progressService = progressService;
            _logger = logger;
        }

        /// <summary>
        /// Получить прогресс операции
        /// </summary>
        [HttpGet("{operationId}")]
        public ActionResult<ProgressViewModel> GetProgress(string operationId)
        {
            try
            {
                var progress = _progressService.GetProgress(operationId);
                return new ProgressViewModel
                {
                    OperationId = progress.OperationId,
                    Progress = progress.Progress,
                    Status = progress.Status,
                    IsCompleted = progress.IsCompleted,
                    IsError = progress.IsError,
                    Result = progress.Result
                };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, $"Попытка получить несуществующую операцию: {operationId}");
                return NotFound(new { message = $"Операция {operationId} не найдена" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении прогресса операции {operationId}");
                return StatusCode(500, new { message = $"Ошибка: {ex.Message}" });
            }
        }
    }
}

// DocumentManager.Web/Models/ProgressViewModel.cs
namespace DocumentManager.Web.Models
{
    /// <summary>
    /// Модель представления прогресса операции
    /// </summary>
    public class ProgressViewModel
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
        /// Результат операции (например, URL для скачивания файла)
        /// </summary>
        public string Result { get; set; }
    }
}