using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

public sealed class AIRecommendationRepository : Repository<AIRecommendation>, IAIRecommendationRepository
{
    public AIRecommendationRepository(DbContext context) : base(context) { }

    public async Task<int> CountForChildAsync(Guid childId, CancellationToken cancellationToken = default) =>
        await Set.CountAsync(r => r.ChildId == childId, cancellationToken);
}
