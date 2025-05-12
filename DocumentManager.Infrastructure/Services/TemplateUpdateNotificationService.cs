// DocumentManager.Infrastructure/Services/TemplateUpdateNotificationService.cs
using DocumentManager.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DocumentManager.Infrastructure.Services
{
    /// <summary>
    /// Сервис для управления уведомлениями об обновлениях шаблонов
    /// </summary>
    public class TemplateUpdateNotificationService
    {
        private readonly ILogger<TemplateUpdateNotificationService> _logger;

        // Хранилище активных уведомлений
        private readonly ConcurrentDictionary<string, TemplateUpdateNotification> _activeNotifications;

        public TemplateUpdateNotificationService(ILogger<TemplateUpdateNotificationService> logger)
        {
            _logger = logger;
            _activeNotifications = new ConcurrentDictionary<string, TemplateUpdateNotification>();
        }

        /// <summary>
        /// Создание уведомления об обновлениях шаблонов
        /// </summary>
        public async Task<string> CreateTemplateUpdateNotificationAsync(List<TemplateVersionUpdate> updates)
        {
            var notificationId = Guid.NewGuid().ToString();

            var notification = new TemplateUpdateNotification
            {
                Id = notificationId,
                Updates = updates,
                CreatedAt = DateTime.UtcNow,
                IsVisible = true,
                IsDismissed = false
            };

            _activeNotifications[notificationId] = notification;

            _logger.LogInformation($"Создано уведомление {notificationId} для {updates.Count} обновлений");

            return notificationId;
        }

        /// <summary>
        /// Получение активных уведомлений
        /// </summary>
        public List<TemplateUpdateNotification> GetActiveNotifications()
        {
            return _activeNotifications.Values
                .Where(n => n.IsVisible && !n.IsDismissed)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Скрытие уведомления
        /// </summary>
        public bool DismissNotification(string notificationId)
        {
            if (_activeNotifications.TryGetValue(notificationId, out var notification))
            {
                notification.IsDismissed = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Очистка старых уведомлений
        /// </summary>
        public void CleanupOldNotifications(TimeSpan maxAge)
        {
            var cutoffTime = DateTime.UtcNow - maxAge;

            var oldNotifications = _activeNotifications.Values
                .Where(n => n.CreatedAt < cutoffTime || n.IsDismissed)
                .ToList();

            foreach (var notification in oldNotifications)
            {
                _activeNotifications.TryRemove(notification.Id, out _);
            }

            if (oldNotifications.Any())
            {
                _logger.LogInformation($"Удалено {oldNotifications.Count} старых уведомлений");
            }
        }
    }

    /// <summary>
    /// Модель уведомления об обновлениях шаблонов
    /// </summary>
    public class TemplateUpdateNotification
    {
        public string Id { get; set; }
        public List<TemplateVersionUpdate> Updates { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsVisible { get; set; }
        public bool IsDismissed { get; set; }

        public string GetMessage()
        {
            if (Updates.Count == 1)
            {
                var update = Updates.First();
                return $"Обнаружено обновление шаблона {update.TemplateCode}";
            }
            else
            {
                return $"Обнаружено {Updates.Count} обновлений шаблонов";
            }
        }
    }
}