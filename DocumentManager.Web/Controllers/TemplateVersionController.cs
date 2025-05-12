// DocumentManager.Web/Controllers/TemplateVersionController.cs
using DocumentManager.Infrastructure.Services;
using DocumentManager.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManager.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TemplateVersionController : ControllerBase
    {
        private readonly TemplateVersionMonitoringService _monitoringService;
        private readonly TemplateUpdateNotificationService _notificationService;
        private readonly ILogger<TemplateVersionController> _logger;

        public TemplateVersionController(
            TemplateVersionMonitoringService monitoringService,
            TemplateUpdateNotificationService notificationService,
            ILogger<TemplateVersionController> logger)
        {
            _monitoringService = monitoringService;
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Принудительная проверка версий шаблонов
        /// </summary>
        [HttpPost("check")]
        public async Task<IActionResult> CheckForUpdates()
        {
            try
            {
                _logger.LogInformation("Запрошена принудительная проверка версий шаблонов");

                var updates = await _monitoringService.CheckForTemplateUpdatesAsync();

                if (updates.Any())
                {
                    var notificationId = await _notificationService.CreateTemplateUpdateNotificationAsync(updates);

                    return Ok(new
                    {
                        success = true,
                        message = $"Обнаружено {updates.Count} обновлений",
                        notificationId = notificationId,
                        updates = updates.Select(u => new
                        {
                            templateCode = u.TemplateCode,
                            oldVersion = u.OldVersion,
                            newVersion = u.NewVersion,
                            oldPath = u.OldPath,
                            newPath = u.NewPath
                        })
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Новых версий шаблонов не обнаружено"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке версий шаблонов");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Применение обновлений шаблонов
        /// </summary>
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyUpdates([FromBody] ApplyUpdatesRequest request)
        {
            try
            {
                var notification = _notificationService.GetActiveNotifications()
                    .FirstOrDefault(n => n.Id == request.NotificationId);

                if (notification == null)
                {
                    return NotFound("Уведомление не найдено");
                }

                var success = await _monitoringService.ApplyTemplateUpdatesAsync(notification.Updates);

                if (success)
                {
                    // Скрываем уведомление после успешного применения
                    _notificationService.DismissNotification(request.NotificationId);

                    return Ok(new
                    {
                        success = true,
                        message = "Обновления успешно применены",
                        appliedCount = notification.Updates.Count
                    });
                }
                else
                {
                    return StatusCode(500, new { error = "Ошибка при применении обновлений" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при применении обновлений");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Получение активных уведомлений
        /// </summary>
        [HttpGet("notifications")]
        public IActionResult GetNotifications()
        {
            var notifications = _notificationService.GetActiveNotifications();

            return Ok(notifications.Select(n => new
            {
                id = n.Id,
                message = n.GetMessage(),
                createdAt = n.CreatedAt,
                updatesCount = n.Updates.Count,
                updates = n.Updates.Select(u => new
                {
                    templateCode = u.TemplateCode,
                    oldVersion = u.OldVersion,
                    newVersion = u.NewVersion
                })
            }));
        }

        /// <summary>
        /// Скрытие уведомления
        /// </summary>
        [HttpPost("notifications/{notificationId}/dismiss")]
        public IActionResult DismissNotification(string notificationId)
        {
            var success = _notificationService.DismissNotification(notificationId);

            if (success)
            {
                return Ok(new { success = true });
            }
            else
            {
                return NotFound("Уведомление не найдено");
            }
        }
    }

    public class ApplyUpdatesRequest
    {
        public string NotificationId { get; set; }
    }
}