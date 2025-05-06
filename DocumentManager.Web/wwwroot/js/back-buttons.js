/**
 * Универсальный обработчик для кнопок "Назад"
 * Умеет:
 * - Возвращаться на предыдущую страницу в истории браузера
 * - Переходить на резервный URL, если истории нет
 * - Поддерживать сохранение состояния фильтров и параметров поиска
 */
(function () {
    // Инициализация при загрузке DOM
    document.addEventListener('DOMContentLoaded', function () {
        initializeBackButtons();

        // Если нужна динамическая инициализация кнопок, добавленных после загрузки страницы (AJAX)
        document.addEventListener('DOMNodeInserted', function (e) {
            if (e.target.querySelector && e.target.querySelector('.back-button')) {
                initializeBackButtons();
            }
        });
    });

    // Основная функция инициализации кнопок
    function initializeBackButtons() {
        const backButtons = document.querySelectorAll('.back-button');

        backButtons.forEach(function (button) {
            // Избегаем повторной инициализации
            if (button.getAttribute('data-initialized') === 'true') {
                return;
            }

            // Слушатель события клика
            button.addEventListener('click', handleBackButtonClick);

            // Отмечаем кнопку как инициализированную
            button.setAttribute('data-initialized', 'true');
        });
    }

    // Обработчик клика
    function handleBackButtonClick(event) {
        event.preventDefault();

        const button = event.currentTarget;
        const fallbackUrl = button.getAttribute('data-fallback-url') || '/';
        const preserveQueryString = button.getAttribute('data-preserve-query') === 'true';

        // Если в истории есть предыдущая страница с текущего домена
        if (document.referrer && document.referrer.includes(window.location.hostname)) {
            // Если нужно сохранить параметры запроса
            if (preserveQueryString && window.location.search) {
                const referrerUrl = new URL(document.referrer);
                const currentParams = new URLSearchParams(window.location.search);

                // Перебираем параметры текущего URL
                for (const [key, value] of currentParams.entries()) {
                    // Добавляем их к URL referrer
                    if (!referrerUrl.searchParams.has(key)) {
                        referrerUrl.searchParams.append(key, value);
                    }
                }

                window.location.href = referrerUrl.toString();
            } else {
                // Обычный возврат назад
                window.history.back();
            }
        } else {
            // Если нет истории, используем fallback URL
            window.location.href = fallbackUrl;
        }
    }

    // Экспорт функции для использования в других скриптах
    window.initializeBackButtons = initializeBackButtons;
})();