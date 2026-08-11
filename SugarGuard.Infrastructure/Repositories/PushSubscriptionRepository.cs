using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class PushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly DbContext _db;

    public PushSubscriptionRepository(DbContext db) => _db = db;

    public async Task<IReadOnlyList<PushSubscription>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default) =>
        await _db.Set<PushSubscription>()
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

    public Task<PushSubscription?> GetByEndpointAsync(
        string endpoint,
        CancellationToken ct = default) =>
        _db.Set<PushSubscription>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

    public async Task<PushSubscriptionUpsertResult> UpsertForUserAsync(
        PushSubscription subscription,
        int maximumSubscriptionsPerUser,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumSubscriptionsPerUser, 1);

        var set = _db.Set<PushSubscription>();
        var existing = await set
            .FirstOrDefaultAsync(s => s.Endpoint == subscription.Endpoint, ct);

        if (existing is not null)
        {
            if (existing.UserId != subscription.UserId)
            {
                return PushSubscriptionUpsertResult.EndpointOwnedByAnotherUser;
            }

            set.Remove(existing);

            set.Add(subscription);
            await _db.SaveChangesAsync(ct);
            return PushSubscriptionUpsertResult.Updated;
        }

        var subscriptionCount = await set.CountAsync(s => s.UserId == subscription.UserId, ct);
        if (subscriptionCount >= maximumSubscriptionsPerUser)
        {
            return PushSubscriptionUpsertResult.LimitExceeded;
        }

        set.Add(subscription);
        await _db.SaveChangesAsync(ct);
        return PushSubscriptionUpsertResult.Created;
    }

    public async Task<bool> RemoveByEndpointAsync(
        string endpoint,
        Guid userId,
        CancellationToken ct = default)
    {
        var set = _db.Set<PushSubscription>();
        var sub = await set
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId, ct);

        if (sub is null) return false;

        set.Remove(sub);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<PushSubscription>> GetAllActiveAsync(CancellationToken ct = default) =>
        await _db.Set<PushSubscription>().AsNoTracking().ToListAsync(ct);
}
