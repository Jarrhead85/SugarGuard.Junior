using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class DoctorNoteRepository : Repository<DoctorNote>, IDoctorNoteRepository
{
    public DoctorNoteRepository(DbContext context) : base(context) { }

    public async Task<IReadOnlyList<DoctorNote>> GetByChildAsync(
        Guid childId, int skip, int take, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(n => n.ChildId == childId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByChildAsync(Guid childId, CancellationToken cancellationToken = default) =>
        await Set.CountAsync(n => n.ChildId == childId, cancellationToken);
}
