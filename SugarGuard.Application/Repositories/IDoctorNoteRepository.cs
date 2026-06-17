using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface IDoctorNoteRepository : IRepository<DoctorNote>
{
    Task<IReadOnlyList<DoctorNote>> GetByChildAsync(Guid childId, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> CountByChildAsync(Guid childId, CancellationToken cancellationToken = default);
}
