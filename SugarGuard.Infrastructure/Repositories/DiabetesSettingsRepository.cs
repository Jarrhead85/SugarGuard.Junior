using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class DiabetesSettingsRepository : Repository<DiabetesSettings>, IDiabetesSettingsRepository
{
    public DiabetesSettingsRepository(DbContext context) : base(context) { }

    public async Task<DiabetesSettings?> GetByChildAsync(Guid childId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().FirstOrDefaultAsync(s => s.ChildId == childId, cancellationToken);
}
