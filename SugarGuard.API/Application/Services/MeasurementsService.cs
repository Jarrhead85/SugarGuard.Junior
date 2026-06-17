using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация измерения глюкозы
/// </summary>
public class MeasurementsService : IMeasurementsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _audit;
    private readonly ILogger<MeasurementsService> _logger;

    public MeasurementsService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService audit,
        ILogger<MeasurementsService> logger)
    {
        _dbFactory = dbFactory;
        _audit = audit;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> ChildExistsAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Children
            .AsNoTracking()
            .AnyAsync(c => c.ChildId == childId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Child?> GetChildAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Children
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ChildId == childId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Measurement> CreateAsync(
        CreateMeasurementRequest request,
        CancellationToken cancellationToken = default)
    {
        var measurement = new Measurement
        {
            ChildId = request.ChildId,
            GlucoseValue = request.GlucoseValue,
            MeasurementTime = request.MeasurementTime,
            ChildState = request.ChildState,
            Notes = request.Notes,
            DataSource = request.DataSource
        };

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.Measurements.Add(measurement);
            await db.SaveChangesAsync(cancellationToken);
        }

        await _audit.WriteAsync(
            action: "measurement.created",
            targetType: "Measurement",
            targetId: measurement.MeasurementId.ToString(),
            details: $"Child={measurement.ChildId};Glucose={measurement.GlucoseValue}",
            cancellationToken: CancellationToken.None);

        return measurement;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Measurement>> GetByChildAsync(
        Guid childId,
        DateTime? from,
        DateTime? to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 1000);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Measurements
            .AsNoTracking()
            .Where(m => m.ChildId == childId);

        if (from.HasValue)
            query = query.Where(m => m.MeasurementTime >= from.Value);

        if (to.HasValue)
            query = query.Where(m => m.MeasurementTime <= to.Value);

        return await query
            .OrderByDescending(m => m.MeasurementTime)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Measurement?> GetLatestAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Measurements
            .AsNoTracking()
            .Where(m => m.ChildId == childId)
            .OrderByDescending(m => m.MeasurementTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Measurement?> GetByIdAsync(
        Guid measurementId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Measurements
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MeasurementId == measurementId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Measurement>> GetForStatisticsAsync(
        Guid childId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Measurements
            .AsNoTracking()
            .Where(m => m.ChildId == childId
                     && m.MeasurementTime >= fromDate
                     && m.MeasurementTime <= toDate)
            .OrderByDescending(m => m.MeasurementTime)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SyncMeasurementsResult> SyncBatchAsync(
        SyncMeasurementsRequest request,
        Func<CancellationToken, Task<IReadOnlyList<Guid>>> getAccessibleChildIdsAsync,
        CancellationToken cancellationToken = default)
    {
        if (request.Measurements.Count == 0)
        {
            return new SyncMeasurementsResult(0, 0, Array.Empty<SyncConflictDto>());
        }

        if (request.Measurements.Count > 1000)
        {
            return new SyncMeasurementsResult(0, 0, Array.Empty<SyncConflictDto>());
        }

        var childIds = request.Measurements.Select(m => m.ChildId).Distinct().ToHashSet();

        HashSet<Guid> existingChildIds;
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            existingChildIds = await db.Children
                .AsNoTracking()
                .Where(c => childIds.Contains(c.ChildId))
                .Select(c => c.ChildId)
                .ToHashSetAsync(cancellationToken);
        }

        var accessibleChildIds = (await getAccessibleChildIdsAsync(cancellationToken))
            .ToHashSet();

        List<(Guid ChildId, DateTime MeasurementTime, decimal GlucoseValue, Guid MeasurementId, DateTime CreatedAt)>
            existingMeasurements;
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            existingMeasurements = await db.Measurements
                .AsNoTracking()
                .Where(m => childIds.Contains(m.ChildId))
                .Select(m => new
                {
                    m.ChildId,
                    m.MeasurementTime,
                    m.GlucoseValue,
                    m.MeasurementId,
                    m.CreatedAt
                })
                .ToListAsync(cancellationToken)
                .ContinueWith(t => t.Result
                    .Select(x => (x.ChildId, x.MeasurementTime, x.GlucoseValue, x.MeasurementId, x.CreatedAt))
                    .ToList(), cancellationToken);
        }

        var existingKeys = existingMeasurements
            .Select(m => (m.ChildId, m.MeasurementTime, m.GlucoseValue))
            .ToHashSet();

        var existingMap = existingMeasurements.ToDictionary(
            m => (m.ChildId, m.MeasurementTime, m.GlucoseValue),
            m => (m.MeasurementId, m.CreatedAt));

        var successCount = 0;
        var errorCount = 0;
        var conflicts = new List<SyncConflictDto>();
        var newMeasurements = new List<Measurement>();
        var syncLogs = new List<SyncLog>();

        foreach (var item in request.Measurements)
        {
            // Ребёнок не существует или нет доступа
            if (!accessibleChildIds.Contains(item.ChildId))
            {
                errorCount++;
                syncLogs.Add(new SyncLog
                {
                    ChildId = item.ChildId,
                    EntityType = "Measurement",
                    EntityId = item.ClientOperationId ?? Guid.NewGuid().ToString(),
                    Status = "failed",
                    Error = existingChildIds.Contains(item.ChildId)
                        ? "Access denied"
                        : "Child not found",
                    IsConflict = false,
                    CreatedAt = DateTime.UtcNow
                });
                continue;
            }

            var key = (item.ChildId, item.MeasurementTime, item.GlucoseValue);

            // Дубликат — фиксируем конфликт
            if (existingKeys.Contains(key))
            {
                var existing = existingMap[key];
                errorCount++;

                conflicts.Add(new SyncConflictDto
                {
                    EntityId = existing.MeasurementId.ToString(),
                    EntityType = "Measurement",
                    ServerModifiedAt = existing.CreatedAt,
                    LocalModifiedAt = item.MeasurementTime,
                    ServerVersion = $"{{\"measurementId\":\"{existing.MeasurementId}\"}}",
                    WinningVersion = "Server",
                    ResolutionStrategy = SyncResolutionStrategy.ServerWinsOnDuplicate
                });

                syncLogs.Add(new SyncLog
                {
                    ChildId = item.ChildId,
                    EntityType = "Measurement",
                    EntityId = existing.MeasurementId.ToString(),
                    Status = "conflict",
                    Error = "Duplicate measurement detected",
                    IsConflict = true,
                    CreatedAt = DateTime.UtcNow
                });
                continue;
            }

            var measurement = new Measurement
            {
                ChildId = item.ChildId,
                GlucoseValue = item.GlucoseValue,
                MeasurementTime = item.MeasurementTime,
                ChildState = item.ChildState,
                Notes = item.Notes,
                DataSource = string.IsNullOrWhiteSpace(item.DataSource)
                    ? "mobile_app"
                    : item.DataSource,
                CreatedAt = DateTime.UtcNow
            };

            newMeasurements.Add(measurement);
            syncLogs.Add(new SyncLog
            {
                ChildId = item.ChildId,
                EntityType = "Measurement",
                EntityId = measurement.MeasurementId.ToString(),
                Status = "success",
                IsConflict = false,
                CreatedAt = DateTime.UtcNow
            });
            successCount++;
        }

        // Сохраняем все новые измерения + логи синхронизации за один раз
        if (newMeasurements.Count > 0 || syncLogs.Count > 0)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            db.Measurements.AddRange(newMeasurements);
            db.SyncLogs.AddRange(syncLogs);
            await db.SaveChangesAsync(cancellationToken);
        }

        await _audit.WriteAsync(
            action: "sync.batch_completed",
            targetType: "Measurement",
            targetId: null,
            details: $"Success={successCount};Errors={errorCount};" +
                     $"Conflicts={conflicts.Count};AppVersion={request.AppVersion}",
            cancellationToken: CancellationToken.None);

        _logger.LogInformation(
            "SyncBatchAsync: Success={Success} Errors={Errors} Conflicts={Conflicts}.",
            successCount, errorCount, conflicts.Count);

        return new SyncMeasurementsResult(successCount, errorCount, conflicts);
    }
}
