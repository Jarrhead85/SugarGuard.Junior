using SugarGuard.Domain.Entities;

namespace SugarGuard.Application.Repositories;

public interface IInviteCodeRepository : IRepository<InviteCode>
{
    Task<InviteCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InviteCode>> GetActiveForChildAsync(Guid childId, CancellationToken cancellationToken = default);
}
