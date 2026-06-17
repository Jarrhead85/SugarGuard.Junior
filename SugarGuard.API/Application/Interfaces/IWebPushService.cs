using SugarGuard.API.DTOs;
using SugarGuard.API.Models;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Результат отписки от Push-уведомлений
/// </summary>
public enum UnsubscribeResult
{
    Removed,
    NotFound,
    Forbidden
}

/// <summary>
/// Сервис Web Push-уведомлений
/// </summary>
public interface IWebPushService
{
    /// <summary>
    /// Регистрирует подписку браузера
    /// </summary>
    Task<NotificationResponse> SubscribeAsync(
        PushSubscriptionRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Удаляет подписку по endpoint-URL с проверкой владельца
    /// </summary>
    Task<UnsubscribeResult> UnsubscribeAsync(
        string endpoint, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Отправляет Push-уведомление всем подпискам пользователя
    /// </summary>
    Task SendNotificationAsync(
        Guid userId, string title, string body, string? url = null, CancellationToken ct = default);
}
