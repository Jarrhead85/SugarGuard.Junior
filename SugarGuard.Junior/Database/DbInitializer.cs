// Инициализирует БД при первом запуске
// Создаёт таблицы, применяет миграции
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SugarGuard.Junior.Database;

/// <summary>
/// Инициализация базы данных
/// Должна быть вызвана при первом запуске приложения
/// </summary>
public class DbInitializer
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(IDbContextFactory<AppDbContext> factory, ILogger<DbInitializer> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// Инициализирует БД (создаёт таблицы если нужно)
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation(" Инициализация БД...");

            // Применяем все миграции (или создаём таблицы если их нет)
            await using var ctx = await _factory.CreateDbContextAsync();
            await ctx.Database.MigrateAsync();

            _logger.LogInformation(" БД инициализирована успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации БД");
            throw;
        }
    }

    /// <summary>
    /// Проверяет доступность БД
    /// </summary>
    public async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Очищает всю БД (ОСТОРОЖНО!)
    /// Используется только в тестах
    /// </summary>
    public async Task ClearAsync()
    {
        try
        {
            _logger.LogWarning("Очистка всей БД!");
            await using var ctx = await _factory.CreateDbContextAsync();
            await ctx.Database.EnsureDeletedAsync();
            await ctx.Database.EnsureCreatedAsync();
            _logger.LogWarning("БД очищена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке БД: {Message}", ex.Message);
            throw;
        }
    }
}
