using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface IDiabetesSettingsRepository : IRepository<DiabetesSettings>
{
    Task<DiabetesSettings?> GetByChildAsync(Guid childId, CancellationToken cancellationToken = default);
}
