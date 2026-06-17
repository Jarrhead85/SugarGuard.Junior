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

    public async Task<PushSubscription?> GetByEndpointAsync(
        string endpoint, CancellationToken ct = default) =>
        await _db.Set<PushSubscription>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

    public async Task AddAsync(PushSubscription subscription, CancellationToken ct = default)
    {
        var set = _db.Set<PushSubscription>();
        var existing = await set
            .FirstOrDefaultAsync(s => s.Endpoint == subscription.Endpoint, ct);

        if (existing is not null)
            set.Remove(existing);

        set.Add(subscription);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> RemoveByEndpointAsync(string endpoint, CancellationToken ct = default)
    {
        var set = _db.Set<PushSubscription>();
        var sub = await set
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

        if (sub is null) return false;

        set.Remove(sub);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<PushSubscription>> GetAllActiveAsync(CancellationToken ct = default) =>
        await _db.Set<PushSubscription>().AsNoTracking().ToListAsync(ct);
}
