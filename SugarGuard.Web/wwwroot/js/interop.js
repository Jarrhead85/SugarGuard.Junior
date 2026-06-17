// SugarGuard.Web/wwwroot/js/interop.js
// Общие Blazor JS Interop хелперы.
// namespace: window.SugarGuard

(function () {
    'use strict';

    /**
     * Программно кликает на DOM-элемент по id.
     * Используется, например, для открытия скрытого <input type="file">
     * из Razor-кода: await JS.InvokeVoidAsync("SugarGuard.clickById", id).
     * @param {string} id
     */
    function clickById(id) {
        const el = document.getElementById(id);
        if (el && typeof el.click === 'function') {
            el.click();
        }
    }

    window.SugarGuard = {
        clickById: clickById
    };

})();
