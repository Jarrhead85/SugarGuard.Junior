// SugarGuard JWT token store — localStorage interop helpers
// All methods are wrapped in try-catch to guard against unavailable localStorage
// (e.g., private browsing mode, storage quota exceeded).

window.tokenStore = {
    getToken: function () {
        try { return localStorage.getItem('sg_access_token'); }
        catch (e) { console.warn('SugarGuard: localStorage недоступен', e); return null; }
    },
    setToken: function (token) {
        try { localStorage.setItem('sg_access_token', token); }
        catch (e) { console.warn('SugarGuard: не удалось сохранить токен', e); }
    },
    removeToken: function () {
        try { localStorage.removeItem('sg_access_token'); }
        catch (e) { }
    },
    getRefreshToken: function () {
        try { return localStorage.getItem('sg_refresh_token'); }
        catch (e) { return null; }
    },
    setRefreshToken: function (token) {
        try { localStorage.setItem('sg_refresh_token', token); }
        catch (e) { console.warn('SugarGuard: не удалось сохранить refresh-токен', e); }
    },
    removeRefreshToken: function () {
        try { localStorage.removeItem('sg_refresh_token'); }
        catch (e) { }
    },
    clearAll: function () {
        try {
            localStorage.removeItem('sg_access_token');
            localStorage.removeItem('sg_refresh_token');
        } catch (e) { }
    }
};
