// Реализация репозитория рюкзака
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.Repositories.Implementations;

/// <summary>
/// Репозиторий для работы с рюкзаком
/// </summary>
public class BackpackRepository : BaseRepository<BackpackItem>, IBackpackRepository
{
    private readonly ICryptoService _cryptoService;

    public BackpackRepository(IDbContextFactory<AppDbContext> factory, ILogger<BackpackRepository> logger, ICryptoService cryptoService)
        : base(factory, logger)
    {
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Получает все активные перекусы ребёнка с опциональной загрузкой связанных данных
    /// </summary>
    public async Task<List<BackpackItem>> GetByChildIdAsync(string childId, bool includeChild = false)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var query = ctx.Set<BackpackItem>()
                .Where(b => b.ChildId == childId);

            // Include navigation properties if requested
            // Note: Navigation properties need to be configured in AppDbContext for Include to work
            // Currently BackpackItem doesn't have navigation properties defined
            // This is prepared for future when navigation properties are added
            if (includeChild)
            {
                // query = query.Include(b => b.Child);
                // Currently commented out as navigation properties are not yet configured
            }
            
            var items = await query
                .OrderByDescending(b => b.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            Logger.LogDebug(" Получено {ItemsCount} перекусов для {ChildId}", items.Count, childId);
            return items;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении рюкзака: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Добавляет перекус в рюкзак
    /// </summary>
    public async Task<BackpackItem> AddSnackAsync(string childId, string snackName, double breadUnits)
    {
        try
        {
            var encryptedSnackName = await _cryptoService.EncryptAsync(snackName);
            var encryptedBreadUnits = await EncryptBreadUnitsAsync(breadUnits);

            var item = new BackpackItem
            {
                BackpackItemId = Guid.NewGuid().ToString(),
                ChildId = childId,
                EncryptedSnackName = encryptedSnackName,
                EncryptedBreadUnits = encryptedBreadUnits,
                CreatedAt = DateTime.UtcNow,
                IsSynced = false
            };

            await using var ctx = await CreateDbContextAsync();
            await ctx.Set<BackpackItem>().AddAsync(item);
            await ctx.SaveChangesAsync();

            Logger.LogInformation(" Перекус добавлен: {SnackName} ({BreadUnits} ХЕ)", snackName, breadUnits);
            return item;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при добавлении перекуса: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Удаляет перекус из рюкзака и добавляет в историю
    /// </summary>
    public async Task<bool> RemoveSnackAsync(string backpackItemId, string childId, string removedBy)
    {
        try
        {
            var item = await GetByIdAsync(backpackItemId);
            if (item == null)
            {
                Logger.LogWarning(" Перекус не найден: {BackpackItemId}", backpackItemId);
                return false;
            }

            await using var ctx = await CreateDbContextAsync();

            // Добавляем в историю
            var history = new BackpackHistory
            {
                HistoryId = Guid.NewGuid().ToString(),
                ChildId = childId,
                EncryptedSnackName = item.EncryptedSnackName, // Копируем уже зашифрованное название
                EncryptedBreadUnits = item.EncryptedBreadUnits, // Копируем зашифрованные ХЕ
                AddedAt = item.CreatedAt,
                DeletedAt = DateTime.UtcNow,
                DeletedBy = removedBy,
                CreatedAt = DateTime.UtcNow
            };

            ctx.Set<BackpackHistory>().Add(history);

            // Удаляем из активных
            ctx.Set<BackpackItem>().Remove(item);
            await ctx.SaveChangesAsync();

            var decryptedName = await _cryptoService.DecryptAsync(item.EncryptedSnackName);
            Logger.LogInformation(" Перекус удалён и добавлен в историю: {DecryptedName}", decryptedName);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при удалении перекуса: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает историю всех перекусов
    /// </summary>
    public async Task<List<BackpackHistory>> GetHistoryAsync(string childId, DateTime startDate, DateTime endDate)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var history = await ctx.Set<BackpackHistory>()
                .Where(h => h.ChildId == childId &&
                       h.DeletedAt >= startDate &&
                       h.DeletedAt <= endDate)
                .OrderByDescending(h => h.DeletedAt)
                .AsNoTracking()
                .ToListAsync();

            Logger.LogDebug(" Получена история ({HistoryCount} записей) за {StartDate:yyyy-MM-dd} - {EndDate:yyyy-MM-dd}", history.Count, startDate, endDate);
            return history;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении истории: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Очищает весь рюкзак ребёнка
    /// </summary>
    public async Task<int> ClearBackpackAsync(string childId)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var items = await ctx.Set<BackpackItem>()
                .Where(b => b.ChildId == childId)
                .ToListAsync();

            ctx.Set<BackpackItem>().RemoveRange(items);
            await ctx.SaveChangesAsync();

            Logger.LogWarning(" Рюкзак очищен ({ItemsCount} перекусов удалено)", items.Count);
            return items.Count;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при очистке рюкзака: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает общее количество углеводов в рюкзаке (read-only с AsNoTracking)
    /// </summary>
    public async Task<double> GetTotalCarbsAsync(string childId)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var items = await ctx.Set<BackpackItem>()
                .Where(b => b.ChildId == childId)
                .AsNoTracking()
                .ToListAsync();

            var tasks = items.Select(item => GetDecryptedBreadUnitsAsync(item));
            var results = await Task.WhenAll(tasks);
            double totalBreadUnits = results.Sum();

            Logger.LogDebug(" Всего хлебных единиц в рюкзаке: {TotalBreadUnits} ХЕ", totalBreadUnits);
            return totalBreadUnits;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при подсчёте хлебных единиц: {Message}", ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Получает расшифрованное название перекуса
    /// </summary>
    public async Task<string> GetDecryptedSnackNameAsync(BackpackItem item)
    {
        try
        {
            return await _cryptoService.DecryptAsync(item.EncryptedSnackName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании названия перекуса: {Message}", ex.Message);
            return "*** ОШИБКА ДЕШИФРОВАНИЯ ***";
        }
    }

    /// <summary>
    /// Получает расшифрованное название перекуса из истории
    /// </summary>
    public async Task<string> GetDecryptedSnackNameAsync(BackpackHistory item)
    {
        try
        {
            return await _cryptoService.DecryptAsync(item.EncryptedSnackName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании названия перекуса из истории: {Message}", ex.Message);
            return "*** ОШИБКА ДЕШИФРОВАНИЯ ***";
        }
    }

    /// <summary>
    /// Получает расшифрованные хлебные единицы
    /// </summary>
    public async Task<double> GetDecryptedBreadUnitsAsync(BackpackItem item)
    {
        if (item == null)
            return 0;
        if (string.IsNullOrWhiteSpace(item.EncryptedBreadUnits))
        {
            Logger.LogDebug("EncryptedBreadUnits пусто для BackpackItem {Id}", item.BackpackItemId);
            return 0;
        }

        try
        {
            var decrypted = await _cryptoService.DecryptAsync(item.EncryptedBreadUnits);
            if (DoubleParser.TryParseDecrypted(decrypted, out var value))
                return value;
            return 0;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Не удалось дешифровать ХЕ для BackpackItem {Id}, пробуем как число", item.BackpackItemId);
            if (DoubleParser.TryParseDecrypted(item.EncryptedBreadUnits, out var fallback))
                return fallback;
            return 0;
        }
    }

    /// <summary>
    /// Получает расшифрованные хлебные единицы из истории
    /// </summary>
    public async Task<double> GetDecryptedBreadUnitsAsync(BackpackHistory item)
    {
        try
        {
            if (string.IsNullOrEmpty(item.EncryptedBreadUnits))
            {
                return 0; // Default value if not encrypted yet
            }

            var decrypted = await _cryptoService.DecryptAsync(item.EncryptedBreadUnits);
            if (DoubleParser.TryParseDecrypted(decrypted, out var value))
            {
                return value;
            }
            return 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании хлебных единиц из истории: {Message}", ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Шифрует хлебные единицы перед сохранением
    /// </summary>
    public async Task<string> EncryptBreadUnitsAsync(double breadUnits)
    {
        try
        {
            return await _cryptoService.EncryptAsync(breadUnits.ToString("F2", CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при шифровании хлебных единиц: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает сумму хлебных единиц, потреблённых сегодня (из SnackConsumptionLog).
    /// </summary>
    public async Task<double> GetConsumedBreadUnitsTodayAsync(string childId)
    {
        try
        {
            var now = DateTime.UtcNow;
            var startOfToday = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
            var startOfTomorrow = startOfToday.AddDays(1);

            await using var ctx = await CreateDbContextAsync();
            var logs = await ctx.Set<SnackConsumptionLog>()
                .Where(l => l.ChildId == childId && l.ConsumedAt >= startOfToday && l.ConsumedAt < startOfTomorrow)
                .AsNoTracking()
                .ToListAsync();

            if (logs.Count == 0)
                return 0;

            var tasks = logs.Select(async log =>
            {
                if (string.IsNullOrEmpty(log.EncryptedBreadUnits)) return 0d;
                try
                {
                    var decrypted = await _cryptoService.DecryptAsync(log.EncryptedBreadUnits);
                    return DoubleParser.TryParseDecrypted(decrypted, out var v) ? v : 0d;
                }
                catch
                {
                    return 0d;
                }
            });
            var values = await Task.WhenAll(tasks);
            return values.Sum();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при подсчёте потреблённых ХЕ за сегодня: {Message}", ex.Message);
            return 0;
        }
    }
}
