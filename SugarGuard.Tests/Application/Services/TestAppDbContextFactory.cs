using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Data;

namespace SugarGuard.Tests.Application.Services;

/// <summary>
/// Test-double для <see cref="IDbContextFactory{TContext}"/>, использующий
/// InMemory provider. Каждый factory создаёт изолированную БД (по имени),
/// чтобы тесты не пересекались.
/// </summary>
public class TestAppDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestAppDbContextFactory(string databaseName)
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;
    }

    public TestAppDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AppDbContext(_options));
}
