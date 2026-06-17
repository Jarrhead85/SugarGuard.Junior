namespace SugarGuard.Web.Shared;

/// <summary>
/// Тип состояния баннера StateBanner
/// </summary>
public enum BannerKind
{
    Loading, // Загрузка данных

    Success, // Успешное выполнение действия

    Warning, // Предупреждение — требует внимания, но не критично

    Error, // Ошибка сети, API или валидации

    Empty, // Пустое состояние — данных нет, но это не ошибка

    SyncPending, // Синхронизация ожидает подключения или обрабатывается

    Offline // Нет подключения к интернету (offline-режим)
}
