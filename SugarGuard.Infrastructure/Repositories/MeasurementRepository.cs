using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class MeasurementRepository : Repository<Measurement>, IMeasurementRepository
{
    public MeasurementRepository(DbContext context) : base(context) { }

    public async Task<Measurement?> GetLatestForChildAsync(Guid childId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(m => m.ChildId == childId)
            .OrderByDescending(m => m.MeasurementTime)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Measurement>> GetForChildAsync(
        Guid childId, DateTime? from, DateTime? to, int limit, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().Where(m => m.ChildId == childId);
        if (from.HasValue) query = query.Where(m => m.MeasurementTime >= from.Value);
        if (to.HasValue) query = query.Where(m => m.MeasurementTime <= to.Value);
        return await query.OrderByDescending(m => m.MeasurementTime).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<int> CountForChildAsync(Guid childId, CancellationToken cancellationToken = default) =>
        await Set.CountAsync(m => m.ChildId == childId, cancellationToken);
}
