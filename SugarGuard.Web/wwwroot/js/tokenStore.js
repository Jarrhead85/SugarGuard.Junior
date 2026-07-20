// Access-токен существует только в памяти страницы. Refresh-токен сохраняется
// сервером в httpOnly cookie и недоступен JavaScript.

let accessToken = null;

window.tokenStore = {
    getToken: function () {
        return accessToken;
    },
    setToken: function (token) {
        accessToken = token || null;
    },
    removeToken: function () {
        accessToken = null;
    },
    setRefreshToken: async function (token) {
        const response = await fetch('/session/refresh-token', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken: token })
        });

        if (!response.ok) {
            throw new Error('Не удалось сохранить сессию.');
        }
    },
    refreshAccessToken: async function (expiredAccessToken) {
        const response = await fetch('/session/refresh', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ accessToken: expiredAccessToken })
        });

        return response.ok ? await response.json() : null;
    },
    removeRefreshToken: async function () {
        await fetch('/session/refresh-token', {
            method: 'DELETE',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ accessToken: accessToken })
        });
    },
    clearAll: function () {
        accessToken = null;
    }
};
