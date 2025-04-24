// Улучшенный интерфейс категорий шаблонов

document.addEventListener('DOMContentLoaded', function () {
    // Инициализация вкладок
    const templateTabs = document.getElementById('templateTabs');
    if (templateTabs) {
        const tabs = new bootstrap.Tab(templateTabs);

        // Сохраняем выбранную вкладку в localStorage
        const tabButtons = document.querySelectorAll('button[data-bs-toggle="tab"]');
        tabButtons.forEach(button => {
            button.addEventListener('shown.bs.tab', function (e) {
                localStorage.setItem('activeTemplateTab', e.target.id);
            });
        });

        // Проверяем, есть ли сохраненная вкладка и активируем её
        var activeTabId = localStorage.getItem('activeTemplateTab');
        if (activeTabId) {
            const tabEl = document.querySelector('#' + activeTabId);
            if (tabEl) {
                const activeTab = new bootstrap.Tab(tabEl);
                activeTab.show();
            }
        }

        // Анимация для карточек при переключении вкладок
        tabButtons.forEach(button => {
            button.addEventListener('shown.bs.tab', function (e) {
                const targetId = e.target.getAttribute('data-bs-target');
                const targetPane = document.querySelector(targetId);

                if (targetPane) {
                    const cards = targetPane.querySelectorAll('.template-card');

                    cards.forEach((card, index) => {
                        card.style.opacity = '0';
                        card.style.transform = 'translateY(20px)';

                        setTimeout(() => {
                            card.style.transition = 'all 0.5s ease';
                            card.style.opacity = '1';
                            card.style.transform = 'translateY(0)';
                        }, 50 + (index * 60));
                    });
                }
            });
        });

        // Добавляем счетчики для каждого типа шаблонов
        const passportPane = document.querySelector('#passports-pane');
        const packingListPane = document.querySelector('#packing-lists-pane');
        const packingInventoryPane = document.querySelector('#packing-inventories-pane');

        if (passportPane) {
            const passportCount = passportPane.querySelectorAll('.template-card').length;
            const passportTab = document.querySelector('#passports-tab');

            if (passportTab && passportCount > 0) {
                const badge = document.createElement('span');
                badge.className = 'ms-2 badge rounded-pill bg-white text-primary';
                badge.textContent = passportCount;
                passportTab.appendChild(badge);
            }
        }

        if (packingListPane) {
            const packingListCount = packingListPane.querySelectorAll('.template-card').length;
            const packingListTab = document.querySelector('#packing-lists-tab');

            if (packingListTab && packingListCount > 0) {
                const badge = document.createElement('span');
                badge.className = 'ms-2 badge rounded-pill bg-white text-success';
                badge.textContent = packingListCount;
                packingListTab.appendChild(badge);
            }
        }

        if (packingInventoryPane) {
            const packingInventoryCount = packingInventoryPane.querySelectorAll('.template-card').length;
            const packingInventoryTab = document.querySelector('#packing-inventories-tab');

            if (packingInventoryTab && packingInventoryCount > 0) {
                const badge = document.createElement('span');
                badge.className = 'ms-2 badge rounded-pill bg-white text-info';
                badge.textContent = packingInventoryCount;
                packingInventoryTab.appendChild(badge);
            }
        }
    }

    // Анимация для первоначальной загрузки карточек
    const initialCards = document.querySelectorAll('.template-card');

    initialCards.forEach((card, index) => {
        card.style.opacity = '0';
        card.style.transform = 'translateY(20px)';

        setTimeout(() => {
            card.style.transition = 'all 0.5s ease';
            card.style.opacity = '1';
            card.style.transform = 'translateY(0)';
        }, 100 + (index * 80));
    });

    // Кнопки с эффектом волны
    const buttons = document.querySelectorAll('.btn');

    buttons.forEach(button => {
        button.addEventListener('click', function (e) {
            const x = e.clientX - e.target.getBoundingClientRect().left;
            const y = e.clientY - e.target.getBoundingClientRect().top;

            const ripple = document.createElement('span');
            ripple.className = 'ripple-effect';
            ripple.style.left = `${x}px`;
            ripple.style.top = `${y}px`;

            this.appendChild(ripple);

            setTimeout(() => {
                ripple.remove();
            }, 600);
        });
    });

    // Добавляем стили для эффекта волны
    const style = document.createElement('style');
    style.textContent = `
        .btn {
            position: relative;
            overflow: hidden;
        }
        
        .ripple-effect {
            position: absolute;
            border-radius: 50%;
            background-color: rgba(255, 255, 255, 0.5);
            width: 100px;
            height: 100px;
            margin-top: -50px;
            margin-left: -50px;
            animation: ripple 0.6s linear;
            transform: scale(0);
            opacity: 0.4;
            pointer-events: none;
        }
        
        @keyframes ripple {
            to {
                transform: scale(3);
                opacity: 0;
            }
        }
    `;

    document.head.appendChild(style);
});