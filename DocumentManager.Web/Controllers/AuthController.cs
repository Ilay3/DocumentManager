// DocumentManager.Web/Controllers/AuthController.cs
using DocumentManager.Web.Models;
using DocumentManager.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManager.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly SimpleAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(SimpleAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        // GET: Auth/Login
        public IActionResult Login(string returnUrl = "/")
        {
            // Если пользователь уже авторизован, перенаправляем на главную
            if (_authService.IsAuthenticated())
            {
                return Redirect(returnUrl);
            }

            // Сохраняем URL для возврата после авторизации
            HttpContext.Session.SetString("ReturnUrl", returnUrl);

            return View(new LoginViewModel());
        }

        // POST: Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Авторизуем пользователя
                    _authService.SetCurrentUser(model.FullName);
                    _logger.LogInformation($"Пользователь авторизован: {model.FullName}");

                    // Получаем URL для возврата
                    var returnUrl = HttpContext.Session.GetString("ReturnUrl") ?? "/";
                    HttpContext.Session.Remove("ReturnUrl");

                    // Перенаправляем на сохраненный URL
                    return Redirect(returnUrl);
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при авторизации пользователя");
                ModelState.AddModelError("", $"Ошибка при авторизации: {ex.Message}");
                return View(model);
            }
        }

        // GET: Auth/Logout
        public IActionResult Logout()
        {
            _authService.Logout();
            return RedirectToAction("Login");
        }
    }
}