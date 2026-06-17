// Design-time factory for EF Core tools (dotnet ef migrations add / script).
// НЕ используется в runtime — только при работе с CLI.
// Использует SQLite in-memory, чтобы избежать необходимости в MAUI FileSystem.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging.Abstractions;

namespace SugarGuard.Junior.Database;

/// <summary>
/// Создаёт <see cref="AppDbContext"/> для <c>dotnet ef</c> tools.
/// <para>
/// В runtime AppDbContext создаётся через DI в MAUI приложении
/// (использует <c>Microsoft.Maui.Storage.FileSystem.AppDataDirectory</c>).
/// </para>
/// <para>
/// Этот factory нужен потому что EF Core tools (dotnet ef) пытаются
/// создать экземпляр контекста при генерации SQL, и без специальной
/// фабрики они упадут на отсутствии MAUI-окружения.
/// </para>
/// </summary>
public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Для design-time используем временный файл, не :memory:,
        // потому что EF Core tools на Windows не всегда корректно работают
        // с in-memory SQLite при чтении схемы через reflection.
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"sugarguard_design_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={tempDbPath}")
            .Options;

        // NullLogger<T> из Microsoft.Extensions.Logging.Abstractions
        // — стандартный no-op логгер, корректно реализует ILogger<AppDbContext>.
        return new AppDbContext(options, NullLogger<AppDbContext>.Instance);
    }
}

