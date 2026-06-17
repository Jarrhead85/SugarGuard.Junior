namespace SugarGuard.Domain.Entities;

/// <summary>
/// Подписка браузера на Web Push-уведомления
/// </summary>
public sealed class PushSubscription
{
    public Guid SubscriptionId { get; init; } = Guid.NewGuid(); // Первичный ключ

    public Guid UserId { get; init; } // ID пользователя-владельца подписки

    public string Endpoint { get; init; } = string.Empty; // Push-эндпоинт браузера

    public string P256Dh { get; init; } = string.Empty;// публичный ключ

    public string Auth { get; init; } = string.Empty; // секрет

    public string? UserAgent { get; init; } // User-Agent браузера 

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow; // Дата создания подписки
   
    public User? User { get; init; } // Навигационное свойство
}
