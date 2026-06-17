using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Data;
using SugarGuard.Domain.Entities;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Сервис для очистки рюкзаков в полночь
/// </summary>
public class BackpackCleanupService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<BackpackCleanupService> _logger;

    public BackpackCleanupService(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<BackpackCleanupService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Выполняет очистку всех рюкзаков в полночь.
    /// </summary>
    public async Task CleanupAllBackpacksAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Начинается ежедневная очистка рюкзаков в полночь");

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        try
        {
            var activeItems = await context.BackpackItems.ToListAsync(ct);

            if (activeItems.Count == 0)
            {
                _logger.LogInformation("Нет активных перекусов для очистки");
                await transaction.CommitAsync(ct);
                return;
            }

            var cleanupTime = DateTime.UtcNow;
            var historyRecords = BuildHistoryRecords(activeItems, cleanupTime);

            context.BackpackHistory.AddRange(historyRecords);
            context.BackpackItems.RemoveRange(activeItems);

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Успешно очищено {ItemCount} перекусов из {ChildCount} рюкзаков",
                activeItems.Count,
                activeItems.Select(i => i.ChildId).Distinct().Count());
        }
        catch (OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Выполняет очистку рюкзака конкретного ребёнка
    /// </summary>
    public async Task CleanupBackpackForChildAsync(Guid childId, CancellationToken ct = default)
    {
        _logger.LogInformation("Начинается очистка рюкзака для ребёнка {ChildId}", childId);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        try
        {
            var activeItems = await context.BackpackItems
                .Where(bi => bi.ChildId == childId)
                .ToListAsync(ct);

            if (activeItems.Count == 0)
            {
                _logger.LogInformation(
                    "Нет активных перекусов для очистки у ребёнка {ChildId}",
                    childId);
                await transaction.CommitAsync(ct);
                return;
            }

            var cleanupTime = DateTime.UtcNow;
            var historyRecords = BuildHistoryRecords(activeItems, cleanupTime);

            context.BackpackHistory.AddRange(historyRecords);
            context.BackpackItems.RemoveRange(activeItems);

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Успешно очищено {ItemCount} перекусов для ребёнка {ChildId}",
                activeItems.Count,
                childId);
        }
        catch (OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Получает статистику последней очистки
    /// </summary>
    public async Task<CleanupStatistics> GetLastCleanupStatisticsAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var lastCleanup = await context.BackpackHistory
                .Where(bh => bh.DeletedBy == BackpackHistoryActor.MidnightCleanup)
                .GroupBy(bh => bh.DeletedAt)
                .OrderByDescending(g => g.Key)
                .Select(g => new CleanupStatistics
                {
                    CleanupDate = g.Key ?? DateTime.MinValue,
                    ItemsCleared = g.Count(),
                    ChildrenAffected = g.Select(h => h.ChildId).Distinct().Count(),
                    TotalBreadUnits = g.Sum(h => h.BreadUnits)
                })
                .FirstOrDefaultAsync();

            return lastCleanup ?? new CleanupStatistics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статистики очистки");
            return new CleanupStatistics();
        }
    }

    private static List<BackpackHistory> BuildHistoryRecords(
        IEnumerable<BackpackItem> activeItems,
        DateTime cleanupTime)
    {
        var records = new List<BackpackHistory>();
        foreach (var item in activeItems)
        {
            records.Add(new BackpackHistory
            {
                ChildId = item.ChildId,
                SnackName = item.SnackName,
                BreadUnits = item.BreadUnits,
                AddedAt = item.CreatedAt,
                DeletedAt = cleanupTime,
                    DeletedBy = BackpackHistoryActor.MidnightCleanup,
                CreatedAt = cleanupTime
            });
        }
        return records;
    }
}

/// <summary>
/// Статистика очистки рюкзаков
/// </summary>
public class CleanupStatistics
{
    public DateTime CleanupDate { get; set; }
    public int ItemsCleared { get; set; }
    public int ChildrenAffected { get; set; }
    public decimal TotalBreadUnits { get; set; }
}
