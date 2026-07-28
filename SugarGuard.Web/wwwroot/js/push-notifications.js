window.SugarGuardPush = (() => {
    // Сервис-воркер должен находиться в корне сайта: браузер не разрешает
    // скрипту из /js/ управлять страницами за пределами /js/.
    const serviceWorkerUrl = '/service-worker.js';

    function isSupported() {
        return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    }

    async function getRegistration() {
        return navigator.serviceWorker.register(serviceWorkerUrl, { scope: '/' });
    }

    function toUint8Array(base64Url) {
        const padding = '='.repeat((4 - (base64Url.length % 4)) % 4);
        const base64 = (base64Url + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw = window.atob(base64);
        return Uint8Array.from(raw, character => character.charCodeAt(0));
    }

    async function sendSubscription(apiBaseUrl, bearerToken, subscription) {
        const data = subscription.toJSON();
        const response = await fetch(`${apiBaseUrl}/api/push/subscribe`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${bearerToken}`
            },
            body: JSON.stringify({
                endpoint: data.endpoint,
                p256dh: data.keys.p256dh,
                auth: data.keys.auth,
                userAgent: navigator.userAgent
            })
        });

        if (!response.ok) {
            throw new Error(`Сервер не сохранил подписку: ${response.status}`);
        }
    }

    async function status() {
        if (!isSupported()) {
            return { supported: false, permission: 'unsupported', subscribed: false };
        }

        const registration = await navigator.serviceWorker.getRegistration('/');
        const subscription = registration
            ? await registration.pushManager.getSubscription()
            : null;

        return {
            supported: true,
            permission: Notification.permission,
            subscribed: subscription !== null
        };
    }

    async function subscribe(vapidPublicKey, apiBaseUrl, bearerToken) {
        if (!isSupported()) {
            throw new Error('Этот браузер не поддерживает Web Push-уведомления.');
        }

        if (!vapidPublicKey || !apiBaseUrl || !bearerToken) {
            throw new Error('Web Push пока не настроен на сервере.');
        }

        let permission = Notification.permission;
        if (permission === 'default') {
            permission = await Notification.requestPermission();
        }

        if (permission !== 'granted') {
            throw new Error('Разрешение на уведомления не предоставлено в браузере.');
        }

        const registration = await getRegistration();
        let subscription = await registration.pushManager.getSubscription();
        if (!subscription) {
            subscription = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: toUint8Array(vapidPublicKey)
            });
        }

        await sendSubscription(apiBaseUrl, bearerToken, subscription);
        return await status();
    }

    async function unsubscribe(apiBaseUrl, bearerToken) {
        if (!isSupported()) {
            return { supported: false, permission: 'unsupported', subscribed: false };
        }

        const registration = await navigator.serviceWorker.getRegistration('/');
        const subscription = registration
            ? await registration.pushManager.getSubscription()
            : null;

        if (!subscription) {
            return await status();
        }

        const response = await fetch(`${apiBaseUrl}/api/push/unsubscribe`, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${bearerToken}`
            },
            body: JSON.stringify({ endpoint: subscription.endpoint })
        });

        if (!response.ok && response.status !== 404) {
            throw new Error(`Сервер не удалил подписку: ${response.status}`);
        }

        await subscription.unsubscribe();
        return await status();
    }

    return { status, subscribe, unsubscribe };
})();
