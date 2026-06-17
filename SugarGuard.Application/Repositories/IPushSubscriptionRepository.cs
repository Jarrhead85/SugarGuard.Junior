using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

/// <summary>
/// Репозиторий Web Push-подписок
/// </summary>
public interface IPushSubscriptionRepository
{
    Task<IReadOnlyList<PushSubscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default); // Возвращает все активные подписки пользователя

    Task<PushSubscription?> GetByEndpointAsync(string endpoint, CancellationToken ct = default); // Возвращает подписку по endpoint-URL

    Task AddAsync(PushSubscription subscription, CancellationToken ct = default); // Добавляет новую подписку

    Task<bool> RemoveByEndpointAsync(string endpoint, CancellationToken ct = default) ;// Удаляет подписку по endpoint-URL без проверки владельца

    Task<IReadOnlyList<PushSubscription>> GetAllActiveAsync(CancellationToken ct = default); // Возвращает все подписки
}
