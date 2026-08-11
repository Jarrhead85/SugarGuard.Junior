using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public enum PushSubscriptionUpsertResult
{
    Created,
    Updated,
    EndpointOwnedByAnotherUser,
    LimitExceeded
}

/// <summary>
/// Репозиторий Web Push-подписок
/// </summary>
public interface IPushSubscriptionRepository
{
    Task<IReadOnlyList<PushSubscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default); // Возвращает все активные подписки пользователя

    Task<PushSubscription?> GetByEndpointAsync(string endpoint, CancellationToken ct = default);

    Task<PushSubscriptionUpsertResult> UpsertForUserAsync(
        PushSubscription subscription,
        int maximumSubscriptionsPerUser,
        CancellationToken ct = default);

    Task<bool> RemoveByEndpointAsync(
        string endpoint,
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<PushSubscription>> GetAllActiveAsync(CancellationToken ct = default); // Возвращает все подписки
}
