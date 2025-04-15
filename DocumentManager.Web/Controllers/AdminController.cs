// DocumentManager.Web/Controllers/AdminController.cs
using DocumentManager.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace DocumentManager.Web.Controllers
{
    public class AdminController : Controller
    {
        private readonly DataInitializer _dataInitializer;
        private readonly ILogger<AdminController> _logger;

        public AdminController(DataInitializer dataInitializer, ILogger<AdminController> logger)
        {
            _dataInitializer = dataInitializer;
            _logger = logger;
        }

        // GET: /Admin/Initialize
        public async Task<IActionResult> Initialize()
        {
            try
            {
                _logger.LogInformation("Начинаем ручную инициализацию данных...");
                await _dataInitializer.InitializeAsync();
                _logger.LogInformation("Ручная инициализация данных выполнена успешно");

                return Content("Инициализация данных выполнена успешно. <a href='/'>Вернуться на главную</a>", "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при ручной инициализации данных");
                return Content($"Ошибка при инициализации данных: {ex.Message}\n\n{ex.StackTrace}", "text/plain");
            }
        }
    }
}