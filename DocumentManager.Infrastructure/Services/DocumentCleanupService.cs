using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentManager.Infrastructure.Services
{
    /// <summary>
    /// Фоновая служба для автоматического удаления устаревших документов
    /// </summary>
    public class DocumentCleanupService : BackgroundService
    {
        private readonly ILogger<DocumentCleanupService> _logger;
        private readonly string _tempDirectory;
        private readonly TimeSpan _deleteAfter;
        private readonly TimeSpan _checkInterval;

        public DocumentCleanupService(
            IConfiguration configuration,
            ILogger<DocumentCleanupService> logger)
        {
            _logger = logger;
            _tempDirectory = configuration["OutputBasePath"];

            // По умолчанию удаляем файлы старше 2 дней и проверяем каждые 12 часов
            _deleteAfter = TimeSpan.FromDays(2);
            _checkInterval = TimeSpan.FromHours(12);

            _logger.LogInformation($"Служба очистки файлов настроена: директория {_tempDirectory}, " +
                $"удаление файлов старше {_deleteAfter.TotalDays} дней, " +
                $"проверка каждые {_checkInterval.TotalHours} часов");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Служба очистки файлов запущена");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOldFilesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при очистке файлов");
                }

                // Ждем следующей проверки
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CleanupOldFilesAsync()
        {
            _logger.LogInformation($"Начата проверка устаревших файлов в {_tempDirectory}");

            if (!Directory.Exists(_tempDirectory))
            {
                _logger.LogWarning($"Директория {_tempDirectory} не существует. Создаем...");
                Directory.CreateDirectory(_tempDirectory);
                return;
            }

            var cutoffDate = DateTime.Now.Subtract(_deleteAfter);
            var files = Directory.GetFiles(_tempDirectory, "*.*", SearchOption.AllDirectories);
            int deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        _logger.LogInformation($"Удаление устаревшего файла: {file} (создан {fileInfo.LastWriteTime})");
                        fileInfo.Delete();
                        deletedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка при удалении файла {file}");
                }
            }

            _logger.LogInformation($"Проверка завершена. Удалено {deletedCount} устаревших файлов");
        }
    }
}