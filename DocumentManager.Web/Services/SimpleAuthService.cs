namespace DocumentManager.Web.Services
{
    /// <summary>
    /// Простой сервис авторизации по имени пользователя
    /// </summary>
    public class SimpleAuthService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string UserSessionKey = "CurrentUser";

        public SimpleAuthService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Установить текущего пользователя
        /// </summary>
        public void SetCurrentUser(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new ArgumentException("ФИО пользователя не может быть пустым", nameof(fullName));
            }

            _httpContextAccessor.HttpContext.Session.SetString(UserSessionKey, fullName);
        }

        /// <summary>
        /// Получить текущего пользователя
        /// </summary>
        public string GetCurrentUser()
        {
            return _httpContextAccessor.HttpContext.Session.GetString(UserSessionKey);
        }

        /// <summary>
        /// Проверить, авторизован ли пользователь
        /// </summary>
        public bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(GetCurrentUser());
        }

        /// <summary>
        /// Выход из системы
        /// </summary>
        public void Logout()
        {
            _httpContextAccessor.HttpContext.Session.Remove(UserSessionKey);
        }
    }
}
