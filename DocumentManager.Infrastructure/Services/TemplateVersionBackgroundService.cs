// DocumentManager.Infrastructure/Services/TemplateVersionBackgroundService.cs
using DocumentManager.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentManager.Infrastructure.Services
{
    /// <summary>
    /// Фоновый сервис для автоматической проверки версий шаблонов
    /// </summary>
    public class TemplateVersionBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TemplateVersionBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(3); // Проверка каждые 3 часа

        public TemplateVersionBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<TemplateVersionBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Запущен фоновый сервис проверки версий шаблонов");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckTemplateVersionsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при автоматической проверке версий шаблонов");
                }

                // Ждем следующую проверку
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CheckTemplateVersionsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var monitoringService = scope.ServiceProvider.GetRequiredService<TemplateVersionMonitoringService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<TemplateUpdateNotificationService>();

            try
            {
                _logger.LogInformation("Начинаем автоматическую проверку версий шаблонов");

                var updates = await monitoringService.CheckForTemplateUpdatesAsync();

                if (updates.Any())
                {
                    _logger.LogInformation($"Обнаружено {updates.Count} обновлений шаблонов");

                    // Создаем уведомление для пользователя
                    await notificationService.CreateTemplateUpdateNotificationAsync(updates);
                }
                else
                {
                    _logger.LogInformation("Новых версий шаблонов не обнаружено");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке версий шаблонов");
            }
        }
    }
}