using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация сервиса проверки приложения
/// </summary>
public class HealthService : IHealthService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public HealthService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <inheritdoc/>
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.CanConnectAsync(cancellationToken);
    }
}
