using Microsoft.EntityFrameworkCore;
using SugarGuard.Application.Repositories;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Infrastructure.Repositories;

/// <summary>
/// Репозиторий событий онбординга
/// </summary>
public sealed class OnboardingEventRepository : Repository<OnboardingEvent>, IOnboardingEventRepository
{
    public OnboardingEventRepository(DbContext context) : base(context) { }
}
