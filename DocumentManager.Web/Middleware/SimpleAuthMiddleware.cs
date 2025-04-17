// DocumentManager.Web/Middleware/SimpleAuthMiddleware.cs
using DocumentManager.Web.Services;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace DocumentManager.Web.Middleware
{
    /// <summary>
    /// Промежуточное ПО для проверки авторизации
    /// </summary>
    public class SimpleAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string[] _allowedPaths;

        public SimpleAuthMiddleware(RequestDelegate next)
        {
            _next = next;
            // Пути, которые не требуют авторизации
            _allowedPaths = new[]
            {
                "/auth/login",
                "/auth/logout",
                "/css",
                "/js",
                "/lib",
                "/favicon.ico"
            };
        }

        public async Task InvokeAsync(HttpContext context, SimpleAuthService authService)
        {
            // Проверяем, не является ли запрашиваемый путь исключением
            var path = context.Request.Path.Value;
            if (IsAllowedPath(path))
            {
                await _next(context);
                return;
            }

            // Проверяем авторизацию
            if (!authService.IsAuthenticated())
            {
                // Запоминаем URL для возврата после авторизации
                context.Session.SetString("ReturnUrl", path);
                context.Response.Redirect("/auth/login");
                return;
            }

            await _next(context);
        }

        private bool IsAllowedPath(string path)
        {
            if (path == null)
                return false;

            foreach (var allowedPath in _allowedPaths)
            {
                if (path.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    // Класс-расширение для добавления промежуточного ПО
    public static class SimpleAuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseSimpleAuth(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SimpleAuthMiddleware>();
        }
    }
}