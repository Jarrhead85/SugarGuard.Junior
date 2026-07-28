self.addEventListener('install', () => {
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(clients.claim());
});

// Показывает уведомления, полученные через Web Push.
self.addEventListener('push', event => {
    if (!event.data) {
        return;
    }

    let payload;
    try {
        payload = event.data.json();
    } catch {
        payload = { title: 'SugarGuard', body: event.data.text() };
    }

    const title = payload.title ?? 'SugarGuard';
    const options = {
        body: payload.body ?? '',
        icon: payload.icon ?? '/favicon.png',
        badge: '/favicon.png',
        data: { url: payload.url ?? '/' },
        vibrate: [200, 100, 200],
        requireInteraction: payload.requireInteraction ?? false
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

// Открывает нужную вкладку кабинета по нажатию на уведомление.
self.addEventListener('notificationclick', event => {
    event.notification.close();

    const targetUrl = event.notification.data?.url ?? '/';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(clientList => {
            const existingWindow = clientList.find(client => client.url.includes(targetUrl) && 'focus' in client);
            return existingWindow ? existingWindow.focus() : clients.openWindow(targetUrl);
        })
    );
});
