using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface IDoctorChildLinkRepository : IRepository<DoctorChildLink>
{
    Task<DoctorChildLink?> GetByDoctorAndChildAsync(Guid doctorUserId, Guid childId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid doctorUserId, Guid childId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoctorChildLink>> GetByDoctorAsync(Guid doctorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoctorChildLink>> GetByChildAsync(Guid childId, CancellationToken cancellationToken = default);
}
