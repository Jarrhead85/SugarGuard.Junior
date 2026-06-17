using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Application.Dashboard.Dto;
using SugarGuard.Application.Glucose;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Application.Dashboard
{
    /// <summary>
    /// Реализация дашборда родителя
    /// </summary>
    public sealed class ParentDashboardService : IParentDashboardService
    {
        private const int MinMeasurementsForReliableComparison = 5;
        private const int PatternMinOccurrenceDays = 3;

        private readonly AppDbContext _db;
        private readonly IGlucoseStatusService _glucoseStatus;
        private readonly IGlucoseUiStateService _glucoseUiState;
        private readonly ILogger<ParentDashboardService> _logger;

        public ParentDashboardService(
            AppDbContext db,
            IGlucoseStatusService glucoseStatus,
            IGlucoseUiStateService glucoseUiState,
            ILogger<ParentDashboardService> logger)
        {
            _db = db;
            _glucoseStatus = glucoseStatus;
            _glucoseUiState = glucoseUiState;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<ParentDashboardSummaryDto> GetSummaryAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var from24 = now.AddHours(-24);
            var from48 = now.AddHours(-48);

            // Последнее измерение 
            var latest = await _db.Measurements
                .AsNoTracking()
                .Where(m => m.ChildId == childId)
                .OrderByDescending(m => m.MeasurementTime)
                .Select(m => new { m.GlucoseValue, m.MeasurementTime })
                .FirstOrDefaultAsync(cancellationToken);

            // KPI за 24 ч — всё считается в SQL
            var kpi24 = await ComputePeriodKpiAsync(childId, from24, now, cancellationToken);

            // KPI за предыдущие 24 ч (дельта)
            var kpiPrev = await ComputePeriodKpiAsync(childId, from48, from24, cancellationToken);

            // Хвостовые счётчики
            var pendingSync = await _db.SyncLogs
                .AsNoTracking()
                .CountAsync(s => s.ChildId == childId && s.IsConflict, cancellationToken);

            var pendingExport = await _db.ExportJobs
                .AsNoTracking()
                .CountAsync(j => j.ChildId == childId && j.Status == "queued", cancellationToken);

            var recoCount = await _db.AIRecommendations
                .AsNoTracking()
                .CountAsync(r => r.ChildId == childId, cancellationToken);

            // Дельты
            decimal? avgDelta = kpi24.AverageGlucose.HasValue && kpiPrev.AverageGlucose.HasValue
                ? kpi24.AverageGlucose.Value - kpiPrev.AverageGlucose.Value
                : null;

            double? tirDelta = kpi24.TotalMeasurements > 0 && kpiPrev.TotalMeasurements > 0
                ? kpi24.TimeInRange - kpiPrev.TimeInRange
                : null;

            // Сборка DTO 
            string? latestStatus = latest is null ? null
                : _glucoseStatus.GetGlucoseStatus(latest.GlucoseValue);
            string? latestUiState = latest is null ? null
                : _glucoseUiState.Resolve(latest.GlucoseValue).ToString();

            int? minutesSinceLast = latest is null ? null
                : (int)(now - latest.MeasurementTime).TotalMinutes;

            _logger.LogDebug(
                "GetSummaryAsync: ChildId={ChildId} Latest={Latest} TIR24={Tir}",
                childId, latest?.GlucoseValue, kpi24.TimeInRange);

            return new ParentDashboardSummaryDto
            {
                ChildId = childId,
                LatestGlucose = latest?.GlucoseValue,
                LatestMeasurementTime = latest?.MeasurementTime,
                LatestGlucoseStatus = latestStatus,
                LatestGlucoseUiState = latestUiState,
                MinutesSinceLastMeasurement = minutesSinceLast,

                AverageGlucose24H = kpi24.AverageGlucose,
                TimeInRange24H = kpi24.TimeInRange,
                TimeBelowRange24H = kpi24.TimeBelowRange,
                TimeAboveRange24H = kpi24.TimeAboveRange,
                HypoEpisodes24H = kpi24.HypoEpisodes,
                HyperEpisodes24H = kpi24.HyperEpisodes,
                CriticalEvents24H = kpi24.CriticalEvents,
                TotalMeasurements24H = kpi24.TotalMeasurements,

                AverageGlucoseDelta = avgDelta,
                TimeInRangeDelta = tirDelta,

                PendingSyncConflicts = pendingSync,
                PendingExportJobs = pendingExport,
                RecommendationsCount = recoCount
            };
        }

        /// <inheritdoc />
        public async Task<PeriodStatisticsDto> GetPeriodStatisticsAsync(
            Guid childId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            // Проецируем только GlucoseValue
            var values = await _db.Measurements
                .AsNoTracking()
                .Where(m => m.ChildId == childId
                         && m.MeasurementTime >= from
                         && m.MeasurementTime <= to)
                .Select(m => m.GlucoseValue)
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "GetPeriodStatisticsAsync: ChildId={ChildId} From={From} To={To} Count={Count}",
                childId, from, to, values.Count);

            if (values.Count == 0)
            {
                return new PeriodStatisticsDto
                {
                    ChildId = childId,
                    From = from,
                    To = to
                };
            }

            var count = values.Count;
            var avg = (double)values.Average();
            var min = (double)values.Min();
            var max = (double)values.Max();
            var sd = CalculateStandardDeviation(values, avg);
            var gmi = CalculateGmi(avg);

            // TIR-зоны
            int inTarget = 0;
            int belowRange = 0;
            int aboveRange = 0;
            int critLow = 0;
            int critHigh = 0;
            int hypoEpisodes = 0;
            int hyperEpisodes = 0;
            int critEpisodes = 0;

            foreach (var v in values)
            {
                var d = (double)v;

                if (d < (double)GlucoseLevels.CriticallyLowThreshold) { critLow++; belowRange++; }
                else if (d <= (double)GlucoseLevels.LowThreshold) { belowRange++; }
                else if (d <= (double)GlucoseLevels.HighThreshold) { inTarget++; }
                else if (d < (double)GlucoseLevels.CriticallyHighThreshold) { aboveRange++; }
                else { critHigh++; aboveRange++; }

                if (d <= (double)GlucoseLevels.LowThreshold) hypoEpisodes++;
                if (d >= (double)GlucoseLevels.HighThreshold) hyperEpisodes++;
                if (d < (double)GlucoseLevels.CriticallyLowThreshold
                 || d > (double)GlucoseLevels.CriticallyHighThreshold) critEpisodes++;
            }

            return new PeriodStatisticsDto
            {
                ChildId = childId,
                From = from,
                To = to,
                TotalMeasurements = count,
                AverageGlucose = Math.Round(avg, 2),
                MinGlucose = Math.Round(min, 2),
                MaxGlucose = Math.Round(max, 2),
                StandardDeviation = Math.Round(sd, 2),
                Gmi = Math.Round(gmi, 1),
                TimeInTargetRange = Math.Round(inTarget * 100.0 / count, 1),
                TimeBelowRange = Math.Round(belowRange * 100.0 / count, 1),
                TimeAboveRange = Math.Round(aboveRange * 100.0 / count, 1),
                TimeCriticallyLow = Math.Round(critLow * 100.0 / count, 1),
                TimeCriticallyHigh = Math.Round(critHigh * 100.0 / count, 1),
                HypoEpisodes = hypoEpisodes,
                HyperEpisodes = hyperEpisodes,
                CriticalEpisodes = critEpisodes,
                GeneratedAt = DateTime.UtcNow
            };
        }

        /// <inheritdoc />
        public async Task<PeriodComparisonDto> GetPeriodComparisonAsync(
            Guid childId,
            DateTime currentFrom,
            DateTime currentTo,
            CancellationToken cancellationToken = default)
        {
            var span = currentTo - currentFrom;
            var prevFrom = currentFrom - span;
            var prevTo = currentFrom;

            // Оба периода запрашиваем параллельно
            var currentTask = GetPeriodStatisticsAsync(childId, currentFrom, currentTo, cancellationToken);
            var previousTask = GetPeriodStatisticsAsync(childId, prevFrom, prevTo, cancellationToken);

            await Task.WhenAll(currentTask, previousTask);

            var current = await currentTask;
            var previous = await previousTask;

            var isReliable = current.TotalMeasurements >= MinMeasurementsForReliableComparison
                          && previous.TotalMeasurements >= MinMeasurementsForReliableComparison;

            string? unreliableReason = isReliable ? null
                : current.TotalMeasurements < MinMeasurementsForReliableComparison
                    ? $"Недостаточно данных в текущем периоде (есть {current.TotalMeasurements}, нужно ≥{MinMeasurementsForReliableComparison})."
                    : $"Недостаточно данных в предыдущем периоде (есть {previous.TotalMeasurements}, нужно ≥{MinMeasurementsForReliableComparison}).";

            _logger.LogDebug(
                "GetPeriodComparisonAsync: ChildId={ChildId} IsReliable={Reliable}",
                childId, isReliable);

            return new PeriodComparisonDto
            {
                ChildId = childId,
                Current = current,
                Previous = previous,
                AverageGlucoseDelta = Math.Round(current.AverageGlucose - previous.AverageGlucose, 2),
                TimeInRangeDelta = Math.Round(current.TimeInTargetRange - previous.TimeInTargetRange, 1),
                HypoEpisodesDelta = current.HypoEpisodes - previous.HypoEpisodes,
                HyperEpisodesDelta = current.HyperEpisodes - previous.HyperEpisodes,
                IsReliable = isReliable,
                UnreliableReason = unreliableReason
            };
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TimelineEventDto>> GetTimelineAsync(
            Guid childId,
            DateTime from,
            DateTime to,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            var safeLimit = Math.Clamp(limit, 1, 500);

            // Четыре источника запрашиваются параллельно
            var measurementsTask = _db.Measurements
    .AsNoTracking()
    .Where(m => m.ChildId == childId
             && m.MeasurementTime >= from
             && m.MeasurementTime <= to)
    // Проецируем в анонимный тип
    .Select(m => new
    {
        m.MeasurementId,
        m.MeasurementTime,
        m.GlucoseValue,
        m.DataSource,
        m.Notes
    })
    .ToListAsync(cancellationToken);

            var snacksTask = _db.SnackConsumptionLogs
                .AsNoTracking()
                .Where(s => s.ChildId == childId
                         && s.ConsumedAt >= from
                         && s.ConsumedAt <= to)
                .Select(s => new TimelineEventDto
                {
                    EventId = s.LogId,
                    EventType = TimelineEventType.SnackConsumed,
                    OccurredAt = s.ConsumedAt,
                    SnackName = s.SnackName,
                    BreadUnits = s.BreadUnits,
                    IsImportant = false
                })
                .ToListAsync(cancellationToken);

            // Критические алерты
            var alertsTask = _db.Measurements
                .AsNoTracking()
                .Where(m => m.ChildId == childId
                         && m.MeasurementTime >= from
                         && m.MeasurementTime <= to
                         && (m.GlucoseValue < (decimal)GlucoseLevels.CriticallyLowThreshold
                          || m.GlucoseValue > (decimal)GlucoseLevels.CriticallyHighThreshold))
                .Select(m => new TimelineEventDto
                {
                    EventId = m.MeasurementId,
                    EventType = TimelineEventType.CriticalAlert,
                    OccurredAt = m.MeasurementTime,
                    GlucoseValue = m.GlucoseValue,
                    IsImportant = true,
                    Notes = m.GlucoseValue < (decimal)GlucoseLevels.CriticallyLowThreshold
                        ? "Критически низкий сахар"
                        : "Критически высокий сахар"
                })
                .ToListAsync(cancellationToken);

            await Task.WhenAll(measurementsTask, snacksTask, alertsTask);

            var measurements = await measurementsTask;
            var snacks = await snacksTask;
            var alerts = await alertsTask;

            // Обогащаем измерения в памяти
            var rawMeasurements = await measurementsTask; 

            var enrichedMeasurements = rawMeasurements.Select(m => new TimelineEventDto
            {
                EventId = m.MeasurementId, 
                EventType = TimelineEventType.Measurement,
                OccurredAt = m.MeasurementTime, 
                GlucoseValue = m.GlucoseValue,
                GlucoseUiState = _glucoseUiState.Resolve(m.GlucoseValue).ToString(),
                DataSource = m.DataSource,
                SnackName = null,
                BreadUnits = null,
                Notes = m.Notes,
                IsImportant = false
            }).ToList();

            var combined = enrichedMeasurements
                .Concat(snacks)
                .Concat(alerts)
                .OrderByDescending(e => e.OccurredAt)
                .Take(safeLimit)
                .ToList();

            _logger.LogDebug(
                "GetTimelineAsync: ChildId={ChildId} Events={Count}",
                childId, combined.Count);

            return combined.AsReadOnly();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<GlucosePatternDto>> DetectPatternsAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var from = DateTime.UtcNow.AddDays(-14);

            var raw = await _db.Measurements
                .AsNoTracking()
                .Where(m => m.ChildId == childId && m.MeasurementTime >= from)
                .Select(m => new
                {
                    m.GlucoseValue,
                    m.MeasurementTime.Hour,
                    Day = m.MeasurementTime.Date
                })
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "DetectPatternsAsync: ChildId={ChildId} Measurements14d={Count}",
                childId, raw.Count);

            if (raw.Count < MinMeasurementsForReliableComparison)
                return Array.Empty<GlucosePatternDto>();

            var patterns = new List<GlucosePatternDto>();

            // Группируем по часу
            var byHour = raw.GroupBy(r => r.Hour);

            foreach (var hourGroup in byHour)
            {
                var hour = hourGroup.Key;
                var entries = hourGroup.ToList();

                // Уникальные дни с гипо
                var hypoDays = entries
                    .Where(e => (double)e.GlucoseValue <= (double)GlucoseLevels.LowThreshold)
                    .Select(e => e.Day)
                    .Distinct()
                    .Count();

                if (hypoDays >= PatternMinOccurrenceDays)
                {
                    var avgInWindow = entries
                        .Where(e => (double)e.GlucoseValue <= (double)GlucoseLevels.LowThreshold)
                        .Average(e => (double)e.GlucoseValue);

                    patterns.Add(new GlucosePatternDto
                    {
                        PatternType = hour is >= 0 and <= 5
                            ? PatternType.NocturnalEpisode
                            : PatternType.RecurringHypo,
                        PeakHour = hour,
                        OccurrenceDays = hypoDays,
                        AverageGlucoseInWindow = Math.Round(avgInWindow, 2),
                        Description = $"Низкий сахар в {hour:D2}:00 — {hypoDays} дней из последних 14."
                    });
                }

                // Гипер-паттерн
                var hyperDays = entries
                    .Where(e => (double)e.GlucoseValue >= (double)GlucoseLevels.HighThreshold)
                    .Select(e => e.Day)
                    .Distinct()
                    .Count();

                if (hyperDays >= PatternMinOccurrenceDays)
                {
                    var avgInWindow = entries
                        .Where(e => (double)e.GlucoseValue >= (double)GlucoseLevels.HighThreshold)
                        .Average(e => (double)e.GlucoseValue);

                    patterns.Add(new GlucosePatternDto
                    {
                        PatternType = PatternType.RecurringHyper,
                        PeakHour = hour,
                        OccurrenceDays = hyperDays,
                        AverageGlucoseInWindow = Math.Round(avgInWindow, 2),
                        Description = $"Высокий сахар в {hour:D2}:00 — {hyperDays} дней из последних 14."
                    });
                }
            }

            return patterns
                .OrderByDescending(p => p.OccurrenceDays)
                .ToList()
                .AsReadOnly();
        }

        // Вспомогательные приватные методы
        /// <summary>
        /// Вычисляет KPI за период на уровне SQL
        /// </summary>
        private async Task<PeriodKpi> ComputePeriodKpiAsync(
            Guid childId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken)
        {
            var values = await _db.Measurements
                .AsNoTracking()
                .Where(m => m.ChildId == childId
                         && m.MeasurementTime >= from
                         && m.MeasurementTime <= to)
                .Select(m => m.GlucoseValue)
                .ToListAsync(cancellationToken);

            if (values.Count == 0)
                return new PeriodKpi();

            var count = values.Count;
            var avg = values.Average();

            int inTarget = 0, below = 0, above = 0, hypo = 0, hyper = 0, crit = 0;

            foreach (var v in values)
            {
                var d = (double)v;

                if (d < (double)GlucoseLevels.CriticallyLowThreshold) { below++; crit++; }
                else if (d <= (double)GlucoseLevels.LowThreshold) { below++; }
                else if (d <= (double)GlucoseLevels.HighThreshold) { inTarget++; }
                else if (d < (double)GlucoseLevels.CriticallyHighThreshold) { above++; }
                else { above++; crit++; }

                if (d <= (double)GlucoseLevels.LowThreshold) hypo++;
                if (d >= (double)GlucoseLevels.HighThreshold) hyper++;
            }

            return new PeriodKpi
            {
                TotalMeasurements = count,
                AverageGlucose = avg,
                TimeInRange = Math.Round(inTarget * 100.0 / count, 1),
                TimeBelowRange = Math.Round(below * 100.0 / count, 1),
                TimeAboveRange = Math.Round(above * 100.0 / count, 1),
                HypoEpisodes = hypo,
                HyperEpisodes = hyper,
                CriticalEvents = crit
            };
        }

        /// <summary>
        /// Glucose Management Indicator: GMI = 12.71 + 4.70587 × avg(ммоль/л).
        /// </summary>
        private static double CalculateGmi(double averageGlucoseMmol)
            => 12.71 + 4.70587 * averageGlucoseMmol;

        /// <summary>
        /// Среднеквадратическое отклонение по генеральной совокупности
        /// </summary>
        private static double CalculateStandardDeviation(
            IReadOnlyList<decimal> values,
            double mean)
        {
            if (values.Count <= 1)
                return 0;

            var variance = values.Average(v => Math.Pow((double)v - mean, 2));
            return Math.Sqrt(variance);
        }

        private sealed record PeriodKpi
        {
            public int TotalMeasurements { get; init; }
            public decimal? AverageGlucose { get; init; }
            public double TimeInRange { get; init; }
            public double TimeBelowRange { get; init; }
            public double TimeAboveRange { get; init; }
            public int HypoEpisodes { get; init; }
            public int HyperEpisodes { get; init; }
            public int CriticalEvents { get; init; }
        }
    }
}
