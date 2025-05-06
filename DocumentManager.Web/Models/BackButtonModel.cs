namespace DocumentManager.Web.Models
{
    public class BackButtonModel
    {
        /// <summary>
        /// URL для перехода, если нет истории браузера
        /// </summary>
        public string FallbackUrl { get; set; } = "/";

        /// <summary>
        /// Текст кнопки
        /// </summary>
        public string ButtonText { get; set; } = "Назад";

        /// <summary>
        /// CSS классы кнопки
        /// </summary>
        public string ButtonClass { get; set; } = "btn btn-outline-secondary";

        /// <summary>
        /// Класс иконки Bootstrap
        /// </summary>
        public string Icon { get; set; } = "bi-arrow-left";

        /// <summary>
        /// Флаг сохранения параметров запроса при возврате
        /// </summary>
        public bool PreserveQuery { get; set; } = false;
    }
}   