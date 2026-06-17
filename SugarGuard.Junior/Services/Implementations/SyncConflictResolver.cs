using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Разрешитель конфликтов синхронизации.
/// Стратегия: First Write Wins по UpdatedAt (совпадает с серверной реализацией).
/// </summary>
public class SyncConflictResolver : ISyncConflictResolver
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<SyncConflictResolver> _logger;

    public SyncConflictResolver(
        IDbContextFactory<AppDbContext> factory,
        ILogger<SyncConflictResolver> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// Разрешает конфликт между локальной и серверной версией данных.
    /// Стратегия: First Write Wins по UpdatedAt (client-side, offline-first).
    /// </summary>
    /// <remarks>
    /// Try/catch НЕ используется намеренно: алгоритм построения результата чистый,
    /// а побочный эффект (сохранение истории) уже изолирован в <see cref="SaveConflictHistoryAsync"/>,
    /// который сам подавляет ошибки записи. Исключения из БД или сети пробрасываются
    /// вызывающему коду (SyncService), который решает, продолжать ли синхронизацию.
    /// </remarks>
    public async Task<ConflictResolutionResult> ResolveConflictAsync(
        SyncConflictInfo conflictInfo,
        string localData)
    {
        _logger.LogInformation(
            " Разрешение конфликта для {EntityType} {EntityId}",
            conflictInfo.EntityType, conflictInfo.EntityId);

        // First Write Wins: побеждает запись с более ранним UpdatedAt
        var result = conflictInfo.LocalModifiedAt <= conflictInfo.ServerModifiedAt
            ? new ConflictResolutionResult
            {
                WinningVersion = "Local",
                ShouldUpdateLocal = false,
                ResolvedData = localData,
                Reason = $"First Write Wins — Local is earlier or equal (Local: {conflictInfo.LocalModifiedAt:yyyy-MM-dd HH:mm:ss}, Server: {conflictInfo.ServerModifiedAt:yyyy-MM-dd HH:mm:ss})"
            }
            : new ConflictResolutionResult
            {
                WinningVersion = "Server",
                ShouldUpdateLocal = true,
                ResolvedData = conflictInfo.ServerVersion,
                Reason = $"First Write Wins — Server is earlier (Server: {conflictInfo.ServerModifiedAt:yyyy-MM-dd HH:mm:ss}, Local: {conflictInfo.LocalModifiedAt:yyyy-MM-dd HH:mm:ss})"
            };

        await SaveConflictHistoryAsync(conflictInfo, localData, result);

        _logger.LogInformation(
            " Конфликт разрешён: {WinningVersion} версия победила. Причина: {Reason}",
            result.WinningVersion, result.Reason);

        return result;
    }

    /// <summary>
    /// Сохраняет историю разрешения конфликта в БД
    /// </summary>
    private async Task SaveConflictHistoryAsync(
        SyncConflictInfo conflictInfo,
        string localData,
        ConflictResolutionResult result)
    {
        try
        {
            var conflictHistory = new SyncConflictHistory
            {
                ConflictId = Guid.NewGuid().ToString(),
                EntityId = conflictInfo.EntityId,
                EntityType = conflictInfo.EntityType,
                LocalVersion = localData,
                ServerVersion = conflictInfo.ServerVersion,
                LocalModifiedAt = conflictInfo.LocalModifiedAt,
                ServerModifiedAt = conflictInfo.ServerModifiedAt,
                ResolutionStrategy = SyncResolutionStrategy.FirstWriteWins,
                WinningVersion = result.WinningVersion,
                ResolutionReason = result.Reason,
                ResolvedBy = "SyncConflictResolver",
                ResolvedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.Set<SyncConflictHistory>().Add(conflictHistory);
            await ctx.SaveChangesAsync();

            _logger.LogDebug(
                " История конфликта сохранена: {ConflictId}",
                conflictHistory.ConflictId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при сохранении истории конфликта");
            // Не пробрасываем исключение, чтобы не прерывать синхронизацию
        }
    }

    /// <summary>
    /// Очищает старую историю конфликтов
    /// </summary>
    public async Task<int> CleanupOldConflictHistoryAsync(int daysToKeep = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

            _logger.LogInformation(
                " Очистка истории конфликтов старше {Days} дней (до {CutoffDate:yyyy-MM-dd})",
                daysToKeep, cutoffDate);

            await using var ctx = await _factory.CreateDbContextAsync();
            var oldConflicts = await ctx.Set<SyncConflictHistory>()
                .Where(c => c.ResolvedAt < cutoffDate)
                .ToListAsync();

            if (oldConflicts.Count == 0)
            {
                _logger.LogInformation(" Нет старых конфликтов для удаления");
                return 0;
            }

            ctx.Set<SyncConflictHistory>().RemoveRange(oldConflicts);
            await ctx.SaveChangesAsync();

            _logger.LogInformation(
                " Удалено {Count} старых записей истории конфликтов",
                oldConflicts.Count);

            return oldConflicts.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при очистке истории конфликтов");
            return 0;
        }
    }
}
