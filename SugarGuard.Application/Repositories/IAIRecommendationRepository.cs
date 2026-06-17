using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface IAIRecommendationRepository : IRepository<AIRecommendation>
{
    Task<int> CountForChildAsync(Guid childId, CancellationToken cancellationToken = default);
}
