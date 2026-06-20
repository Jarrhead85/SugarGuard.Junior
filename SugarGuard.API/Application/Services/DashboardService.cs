using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Application.Glucose;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация дашборда: сводка и история измерений ребёнка
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private const int MaxAllowedLimit = 1000;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IGlucoseStatusService _glucoseStatusService;
    private readonly IGlucoseUiStateService _glucoseUiStateService;

    public DashboardService(
        IDbContextFactory<AppDbContext> dbFactory,
        IGlucoseStatusService glucoseStatusService,
        IGlucoseUiStateService glucoseUiStateService)
    {
        _dbFactory = dbFactory;
        _glucoseStatusService = glucoseStatusService;
        _glucoseUiStateService = glucoseUiStateService;
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
    public async Task<DashboardSummaryResponse> GetSummaryAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var latest = await db.Measurements
            .AsNoTracking()
            .Where(m => m.ChildId == childId)
            .OrderByDescending(m => m.MeasurementTime)
            .FirstOrDefaultAsync(cancellationToken);

        var total = await db.Measurements
            .AsNoTracking()
            .CountAsync(m => m.ChildId == childId, cancellationToken);

        var critical = await db.Measurements
            .AsNoTracking()
            .CountAsync(
                m => m.ChildId == childId
                  && (m.GlucoseValue < (decimal)GlucoseLevels.CriticallyLowThreshold
                   || m.GlucoseValue > (decimal)GlucoseLevels.CriticallyHighThreshold),
                cancellationToken);

        var recommendations = await db.AIRecommendations
            .AsNoTracking()
            .CountAsync(r => r.ChildId == childId, cancellationToken);

        var pendingExport = await db.ExportJobs
            .AsNoTracking()
            .CountAsync(j => j.ChildId == childId && j.Status == "queued", cancellationToken);

        var pendingSync = await db.SyncLogs
            .AsNoTracking()
            .CountAsync(s => s.ChildId == childId && s.IsConflict, cancellationToken);

        return new DashboardSummaryResponse
        {
            ChildId = childId,
            LatestGlucose = latest?.GlucoseValue,
            LatestMeasurementTime = latest?.MeasurementTime,
            LatestGlucoseStatus = latest is null
                ? null
                : _glucoseStatusService.GetGlucoseStatus(latest.GlucoseValue),
            LatestGlucoseUiState = latest is null
                ? null
                : _glucoseUiStateService.Resolve(latest.GlucoseValue).ToString(),
            TotalMeasurements = total,
            CriticalEvents = critical,
            RecommendationsCount = recommendations,
            PendingExportJobs = pendingExport,
            PendingSyncConflicts = pendingSync
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DashboardHistoryItemResponse>> GetHistoryAsync(
        Guid childId,
        DateTime? from,
        DateTime? to,
        string? uiState,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, MaxAllowedLimit);
        var hasUiStateFilter = !string.IsNullOrWhiteSpace(uiState);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Measurements
            .AsNoTracking()
            .Where(m => m.ChildId == childId);

        if (from.HasValue)
            query = query.Where(m => m.MeasurementTime >= from.Value);

        if (to.HasValue)
            query = query.Where(m => m.MeasurementTime <= to.Value);

        if (hasUiStateFilter)
        {
            var normalizedState = NormalizeUiState(uiState!);
            query = query.Where(m => m.GlucoseUiState == normalizedState);
        }

        var items = await query
            .OrderByDescending(m => m.MeasurementTime)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        var projected = items.Select(m =>
        {
            var state = m.GlucoseUiState
                ?? _glucoseUiStateService.Resolve(m.GlucoseValue).ToString();
            return new DashboardHistoryItemResponse
            {
                MeasurementId = m.MeasurementId,
                MeasurementTime = m.MeasurementTime,
                GlucoseValue = m.GlucoseValue,
                GlucoseStatus = _glucoseStatusService.GetGlucoseStatus(m.GlucoseValue),
                GlucoseUiState = state,
                IsCritical = _glucoseStatusService.IsCritical(m.GlucoseValue),
                Notes = m.Notes
            };
        });

        return projected.ToList();
    }

    /// <summary>
    /// Нормализует строковое представление к виду, хранимому в БД
    /// </summary>
    private static string NormalizeUiState(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.ToLowerInvariant() switch
        {
            "critical" => "Critical",
            "attention" => "Attention",
            "normal" => "Normal",
            _ => trimmed
        };
    }
}
