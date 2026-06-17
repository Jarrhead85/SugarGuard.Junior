using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация логов синхронзации
/// </summary>
public class SyncLogService : ISyncLogService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _audit;
    private readonly ILogger<SyncLogService> _logger;

    public SyncLogService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService audit,
        ILogger<SyncLogService> logger)
    {
        _dbFactory = dbFactory;
        _audit = audit;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncLogResponse>> GetAsync(
        IReadOnlyCollection<Guid>? childIds,
        bool onlyConflicts,
        int limit,
        DateTime? since,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 1000);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.SyncLogs
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .AsQueryable();

        if (childIds is not null && childIds.Count > 0)
        {
            query = query.Where(l => childIds.Contains(l.ChildId));
        }

        if (onlyConflicts)
        {
            query = query.Where(l => l.IsConflict);
        }

        if (since.HasValue)
        {
            var sinceUtc = since.Value.Kind == DateTimeKind.Utc
                ? since.Value
                : since.Value.ToUniversalTime();
            query = query.Where(l => l.CreatedAt > sinceUtc);
        }

        return await query
            .Take(safeLimit)
            .Select(l => new SyncLogResponse
            {
                SyncLogId = l.SyncLogId,
                ChildId = l.ChildId,
                EntityType = l.EntityType,
                EntityId = l.EntityId,
                Status = l.Status,
                IsConflict = l.IsConflict,
                Error = l.Error,
                ResolutionSource = l.ResolutionSource,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(Domain.Entities.SyncLog? Log, ResolveOneStatus Status)> ResolveAsync(
        Guid id,
        string resolution,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var log = await db.SyncLogs
            .FirstOrDefaultAsync(l => l.SyncLogId == id, cancellationToken);

        if (log is null)
            return (null, ResolveOneStatus.NotFound);

        if (!log.IsConflict)
            return (log, ResolveOneStatus.NotAConflict);

        log.IsConflict = false;
        log.Status = "resolved";
        log.ResolutionSource = SyncLog.ResolutionSourceManual;
        log.Error = resolution == "useClient"
            ? "Разрешено вручную: клиент должен повторить отправку (useClient)."
            : "Разрешено вручную: принята серверная версия (useServer).";

        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "sync_conflict.resolved",
            targetType: "SyncLog",
            targetId: log.SyncLogId.ToString(),
            details: $"Resolution={resolution};ChildId={log.ChildId};EntityType={log.EntityType};EntityId={log.EntityId}",
            cancellationToken: CancellationToken.None);

        _logger.LogInformation(
            "ResolveAsync: конфликт разрешён. SyncLogId={SyncLogId} Resolution={Resolution} ChildId={ChildId}.",
            log.SyncLogId, resolution, log.ChildId);

        return (log, ResolveOneStatus.Success);
    }

    /// <inheritdoc/>
    public async Task<int> ResolveAllAsync(
        IReadOnlyCollection<Guid> childIds,
        CancellationToken cancellationToken = default)
    {
        if (childIds.Count == 0)
            return 0;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var resolved = await db.SyncLogs
            .Where(l => childIds.Contains(l.ChildId) && l.IsConflict)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(l => l.IsConflict, false)
                    .SetProperty(l => l.Status, "resolved")
                    .SetProperty(l => l.ResolutionSource, SyncLog.ResolutionSourceManual)
                    .SetProperty(
                        l => l.Error,
                        "Разрешено пакетно: принята серверная версия (useServer)."),
                cancellationToken);

        if (resolved > 0)
        {
            await _audit.WriteAsync(
                action: "sync_conflict.resolved_all",
                targetType: "SyncLog",
                targetId: "batch",
                details: $"Resolved={resolved}",
                cancellationToken: CancellationToken.None);
        }

        _logger.LogInformation(
            "ResolveAllAsync: разрешено {Count} конфликтов для {Children} детей.",
            resolved, childIds.Count);

        return resolved;
    }
}
