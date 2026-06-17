using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class DoctorChildLinkRepository : Repository<DoctorChildLink>, IDoctorChildLinkRepository
{
    public DoctorChildLinkRepository(DbContext context) : base(context) { }

    public async Task<DoctorChildLink?> GetByDoctorAndChildAsync(
        Guid doctorUserId, Guid childId, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(l => l.DoctorUserId == doctorUserId && l.ChildId == childId, cancellationToken);

    public async Task<bool> ExistsAsync(Guid doctorUserId, Guid childId, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(l => l.DoctorUserId == doctorUserId && l.ChildId == childId, cancellationToken);

    public async Task<IReadOnlyList<DoctorChildLink>> GetByDoctorAsync(
        Guid doctorUserId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(l => l.DoctorUserId == doctorUserId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorChildLink>> GetByChildAsync(
        Guid childId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(l => l.ChildId == childId).ToListAsync(cancellationToken);
}
