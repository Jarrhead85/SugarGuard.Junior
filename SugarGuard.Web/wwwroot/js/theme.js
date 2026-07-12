// SugarGuard.Web/wwwroot/js/theme.js
// Инициализация темы и переключение light/dark mode.
// Вызывается из App.razor через <script src="js/theme.js">.
// Blazor обращается через sugarGuardTheme.toggle() / sugarGuardTheme.getTheme().

(function () {
    'use strict';

    const STORAGE_KEY = 'sg-theme';
    const ATTR = 'data-theme';
    const VISUAL_STYLE_STORAGE_KEY = 'sg-visual-style';
    const VISUAL_STYLE_ATTR = 'data-visual-style';
    const html = document.documentElement;
    const subscribers = new Map();

    /**
     * Определяет тему при старте:
     * 1. localStorage (сохранённый выбор пользователя)
     * 2. prefers-color-scheme (системная настройка)
     * 3. Fallback — light
     */
    function resolveInitialTheme() {
        try {
            const saved = localStorage.getItem(STORAGE_KEY);
            if (saved === 'light' || saved === 'dark') return saved;
        } catch (_) {
            // localStorage недоступен (приватный режим, iframe sandbox)
        }

        return window.matchMedia('(prefers-color-scheme: dark)').matches
            ? 'dark'
            : 'light';
    }

    /**
     * Применяет тему: выставляет data-theme на <html>.
     * @param {string} theme — "light" | "dark"
     */
    function applyTheme(theme) {
        html.setAttribute(ATTR, theme);
    }

    /**
     * Сохраняет выбор в localStorage (best-effort).
     * @param {string} theme — "light" | "dark"
     */
    function persistTheme(theme) {
        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch (_) {
            // Молча игнорируем: функциональность не нарушается
        }
    }

    function resolveInitialVisualStyle() {
        return 'classic';
    }

    function applyVisualStyle(style) {
        html.setAttribute(VISUAL_STYLE_ATTR, 'classic');
    }

    function persistVisualStyle(style) {
        try {
            localStorage.setItem(VISUAL_STYLE_STORAGE_KEY, style);
        } catch (_) {
            // Текущая страница остаётся в классическом стиле даже без localStorage.
        }
    }

    function getThemeState() {
        const theme = html.getAttribute(ATTR) || 'light';
        const systemTheme = window.matchMedia('(prefers-color-scheme: dark)').matches
            ? 'dark'
            : 'light';

        let preference = null;
        try {
            preference = localStorage.getItem(STORAGE_KEY);
        } catch (_) {
            preference = null;
        }

        return {
            theme,
            isDark: theme === 'dark',
            preference,
            systemTheme,
            isFollowingSystem: preference !== 'light' && preference !== 'dark'
        };
    }

    function notifySubscribers(theme) {
        subscribers.forEach((entry, id) => {
            try {
                entry.dotNetRef.invokeMethodAsync(entry.methodName, theme);
            } catch (error) {
                console.warn('SugarGuardTheme subscriber failed', id, error);
            }
        });
    }

    // ── Публичный API для Blazor JS Interop ─────────────

    window.sugarGuardTheme = {

        /**
         * Инициализация темы при загрузке страницы.
         * Вызывается один раз из App.razor.
         */
        init: function () {
            const theme = resolveInitialTheme();
            applyTheme(theme);
            applyVisualStyle(resolveInitialVisualStyle());
        },

        /**
         * Переключает тему на противоположную.
         * Сохраняет выбор в localStorage.
         */
        toggle: function () {
            const current = html.getAttribute(ATTR) || 'light';
            const next = current === 'dark' ? 'light' : 'dark';
            applyTheme(next);
            persistTheme(next);
            notifySubscribers(next);
        },

        /**
         * Возвращает текущую активную тему: "light" | "dark".
         * @returns {string}
         */
        getTheme: function () {
            return html.getAttribute(ATTR) || 'light';
        },

        /**
         * Устанавливает конкретную тему явно.
         * @param {string} theme — "light" | "dark"
         */
        setTheme: function (theme) {
            if (theme !== 'light' && theme !== 'dark') return;
            applyTheme(theme);
            persistTheme(theme);
            notifySubscribers(theme);
        },

        isDark: function () {
            return this.getTheme() === 'dark';
        },

        getState: function () {
            return getThemeState();
        },

        getVisualStyle: function () {
            return html.getAttribute(VISUAL_STYLE_ATTR) || 'classic';
        },

        setVisualStyle: function (style) {
            const normalized = 'classic';
            applyVisualStyle(normalized);
            persistVisualStyle(normalized);
        },

        initToggleButton: function (buttonId) {
            const button = document.getElementById(buttonId);
            if (!button || button.dataset.sgThemeBound === 'true') return;

            button.dataset.sgThemeBound = 'true';
            button.addEventListener('click', () => window.sugarGuardTheme.toggle());
        },

        subscribe: function (id, dotNetRef, methodName) {
            if (!id || !dotNetRef || !methodName) return;
            subscribers.set(id, { dotNetRef, methodName });
        },

        unsubscribe: function (id) {
            subscribers.delete(id);
        }
    };

    window.SugarGuardTheme = window.sugarGuardTheme;

    // Применяем тему немедленно при загрузке скрипта —
    // до рендера Blazor, чтобы не было "вспышки" неверной темы (FOIT).
    window.sugarGuardTheme.init();

})();
