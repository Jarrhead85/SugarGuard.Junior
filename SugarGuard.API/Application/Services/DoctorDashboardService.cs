using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Application.Glucose;
using SugarGuard.Domain.Entities;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация дашборда врача
/// </summary>
public sealed class DoctorDashboardService : IDoctorDashboardService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IGlucoseUiStateService _glucoseUiState;
    private readonly ILogger<DoctorDashboardService> _logger;

    /// <summary>
    /// Период расчёта TIR и критических событий (7 дней)
    /// </summary>
    private static readonly TimeSpan TirWindow = TimeSpan.FromDays(7);

    /// <summary>
    /// Период "сегодня" для групповой сводки (24 часа)
    /// </summary>
    private static readonly TimeSpan TodayWindow = TimeSpan.FromHours(24);

    public DoctorDashboardService(
        IDbContextFactory<AppDbContext> dbFactory,
        IGlucoseUiStateService glucoseUiState,
        ILogger<DoctorDashboardService> logger)
    {
        _dbFactory = dbFactory;
        _glucoseUiState = glucoseUiState;
        _logger = logger;
    }

    // GetPatientsAsync
    /// <inheritdoc/>
    public async Task<IReadOnlyList<DoctorPatientSummaryDto>> GetPatientsAsync(
        Guid doctorUserId,
        string? sortBy,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var windowStart = DateTime.UtcNow.Subtract(TirWindow);

        // Загружаем всех прикреплённых пациентов одним запросом.
        var linkedChildren = await db.DoctorChildLinks
            .AsNoTracking()
            .Where(l => l.DoctorUserId == doctorUserId && l.IsActive)
            .Select(l => new { l.ChildId, l.LinkId })
            .ToListAsync(cancellationToken);
        var linkedChildIds = linkedChildren.Select(link => link.ChildId).ToList();
        var linkIdsByChild = linkedChildren.ToDictionary(link => link.ChildId, link => link.LinkId);

        if (linkedChildIds.Count == 0)
        {
            _logger.LogInformation("Врач {DoctorId}: нет прикреплённых пациентов.", doctorUserId);
            return Array.Empty<DoctorPatientSummaryDto>();
        }

        // Данные о детях
        var children = await db.Children
            .AsNoTracking()
            .Where(c => linkedChildIds.Contains(c.ChildId))
            .Select(c => new
            {
                c.ChildId,
                c.FirstName,
                c.LastName,
                c.DiabetesType,
                c.DateOfBirth
            })
            .ToListAsync(cancellationToken);

        // Последний замер на каждого пациента. Не используем GroupBy + First()
        // в SQL-проекции: в EF Core/Npgsql это может падать с EmptyProjectionMember.
        var measurementSnapshots = await db.Measurements
            .AsNoTracking()
            .Where(m => linkedChildIds.Contains(m.ChildId))
            .Select(m => new
            {
                m.ChildId,
                m.GlucoseValue,
                m.MeasurementTime
            })
            .ToListAsync(cancellationToken);

        var latestByChild = measurementSnapshots
            .GroupBy(m => m.ChildId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(m => m.MeasurementTime).First());

        // Замеры за последние 7 дней для расчёта TIR и критических событий
        var recentMeasurements = await db.Measurements
            .AsNoTracking()
            .Where(m => linkedChildIds.Contains(m.ChildId)
                     && m.MeasurementTime >= windowStart)
            .Select(m => new
            {
                m.ChildId,
                m.GlucoseValue
            })
            .ToListAsync(cancellationToken);

        // Целевые диапазоны на каждого пациента
        var diabetesSettings = await db.DiabetesSettings
            .AsNoTracking()
            .Where(s => linkedChildIds.Contains(s.ChildId))
            .Select(s => new
            {
                s.ChildId,
                s.TargetRangeMin,
                s.TargetRangeMax
            })
            .ToDictionaryAsync(s => s.ChildId, cancellationToken);

        // Группируем замеры в памяти
        var recentByChild = recentMeasurements
            .GroupBy(m => m.ChildId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = children.Select(child =>
        {
            latestByChild.TryGetValue(child.ChildId, out var latest);
            recentByChild.TryGetValue(child.ChildId, out var recent);

            // Целевой диапазон: берём из настроек или дефолт из констант
            var rangeMin = diabetesSettings.TryGetValue(child.ChildId, out var ds)
                ? (double)ds.TargetRangeMin
                : GlucoseLevels.TargetRangeMin;
            var rangeMax = diabetesSettings.TryGetValue(child.ChildId, out ds)
                ? (double)ds.TargetRangeMax
                : GlucoseLevels.TargetRangeMax;

            var totalRecent = recent?.Count ?? 0;
            var inRangeCount = recent?.Count(m =>
                (double)m.GlucoseValue >= rangeMin &&
                (double)m.GlucoseValue <= rangeMax) ?? 0;
            var criticalCount = recent?.Count(m =>
                (double)m.GlucoseValue < GlucoseLevels.CriticallyLowThreshold ||
                (double)m.GlucoseValue > GlucoseLevels.CriticallyHighThreshold) ?? 0;

            var tir = totalRecent > 0
                ? Math.Round((double)inRangeCount / totalRecent * 100.0, 1)
                : 0.0;

            var uiState = latest is null
                ? null
                : _glucoseUiState.Resolve(latest.GlucoseValue).ToString();

            return new DoctorPatientSummaryDto
            {
                LinkId = linkIdsByChild[child.ChildId],
                ChildId = child.ChildId,
                FirstName = child.FirstName,
                LastName = child.LastName,
                DiabetesType = child.DiabetesType,
                DateOfBirth = child.DateOfBirth,
                LatestGlucose = latest?.GlucoseValue,
                LatestMeasurementTime = latest?.MeasurementTime,
                LatestGlucoseUiState = uiState,
                TimeInTargetRange = tir,
                CriticalEventsLast7Days = criticalCount,
                MeasurementsLast7Days = totalRecent
            };
        }).ToList();

        // Сортировка
        IEnumerable<DoctorPatientSummaryDto> sorted = sortBy?.ToLowerInvariant() switch
        {
            "tir" => result.OrderBy(p => p.TimeInTargetRange),
            "lastmeasurement" => result.OrderByDescending(p => p.LatestMeasurementTime),
            "name" => result.OrderBy(p => p.LastName).ThenBy(p => p.FirstName),
            _ => result.OrderByDescending(p => p.LatestMeasurementTime)
        };

        return sorted.ToList();
    }

    // GetNotesAsync
    /// <inheritdoc/>
    public async Task<IReadOnlyList<DoctorNoteDto>> GetNotesAsync(
        Guid doctorUserId,
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var notes = await db.DoctorNotes
            .AsNoTracking()
            .Where(n => n.DoctorUserId == doctorUserId && n.ChildId == childId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new DoctorNoteDto
            {
                NoteId = n.NoteId,
                ChildId = n.ChildId,
                DoctorUserId = n.DoctorUserId,
                MeasurementId = n.MeasurementId,
                NoteText = n.NoteText,
                IsImportant = n.IsImportant,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return notes;
    }

    // AddNoteAsync
    /// <inheritdoc/>
    public async Task<DoctorNoteDto> AddNoteAsync(
        Guid doctorUserId,
        Guid childId,
        AddDoctorNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Проверяем, что врач действительно прикреплён к этому пациенту
        var isLinked = await db.DoctorChildLinks
            .AsNoTracking()
            .AnyAsync(l => l.DoctorUserId == doctorUserId
                          && l.ChildId == childId
                          && l.IsActive,
                cancellationToken);

        if (!isLinked)
        {
            _logger.LogWarning(
                "Врач {DoctorId} попытался добавить заметку к неприкреплённому пациенту {ChildId}.",
                doctorUserId, childId);
            throw new UnauthorizedAccessException(
                $"Врач {doctorUserId} не прикреплён к пациенту {childId}.");
        }

        // Если указан MeasurementId, то проверяем, что замер принадлежит этому пациенту
        if (request.MeasurementId.HasValue)
        {
            var measurementExists = await db.Measurements
                .AsNoTracking()
                .AnyAsync(m => m.MeasurementId == request.MeasurementId.Value
                            && m.ChildId == childId,
                    cancellationToken);

            if (!measurementExists)
            {
                _logger.LogWarning(
                    "Замер {MeasurementId} не найден или не принадлежит пациенту {ChildId}.",
                    request.MeasurementId.Value, childId);
                throw new ArgumentException(
                    $"Замер {request.MeasurementId} не найден для пациента {childId}.");
            }
        }

        var note = new DoctorNote
        {
            NoteId = Guid.NewGuid(),
            ChildId = childId,
            DoctorUserId = doctorUserId,
            MeasurementId = request.MeasurementId,
            NoteText = request.Text.Trim(),
            IsImportant = request.IsImportant,
            CreatedAt = DateTime.UtcNow
        };

        db.DoctorNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Врач {DoctorId} добавил заметку {NoteId} к пациенту {ChildId}. IsFlag={IsFlag}.",
            doctorUserId, note.NoteId, childId, note.IsImportant);

        return new DoctorNoteDto
        {
            NoteId = note.NoteId,
            ChildId = note.ChildId,
            DoctorUserId = note.DoctorUserId,
            MeasurementId = note.MeasurementId,
            NoteText = note.NoteText,
            IsImportant = note.IsImportant,
            CreatedAt = note.CreatedAt
        };
    }

    // GetCohortSummaryAsync
    /// <inheritdoc/>
    public async Task<DoctorCohortSummaryDto> GetCohortSummaryAsync(
        Guid doctorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var tirWindowStart = DateTime.UtcNow.Subtract(TirWindow);
        var todayWindowStart = DateTime.UtcNow.Subtract(TodayWindow);

        var linkedChildIds = await db.DoctorChildLinks
            .AsNoTracking()
            .Where(l => l.DoctorUserId == doctorUserId && l.IsActive)
            .Select(l => l.ChildId)
            .ToListAsync(cancellationToken);

        if (linkedChildIds.Count == 0)
        {
            return new DoctorCohortSummaryDto
            {
                TotalPatients = 0,
                PatientsWithCriticalToday = 0,
                AverageTimeInTargetRange = 0,
                PatientsWithoutMeasurementsToday = 0,
                GeneratedAt = DateTime.UtcNow
            };
        }

        // Целевые диапазоны по всей когорте
        var allSettings = await db.DiabetesSettings
            .AsNoTracking()
            .Where(s => linkedChildIds.Contains(s.ChildId))
            .ToDictionaryAsync(s => s.ChildId, cancellationToken);

        // Замеры за 7 дней для TIR
        var recentMeasurements = await db.Measurements
            .AsNoTracking()
            .Where(m => linkedChildIds.Contains(m.ChildId)
                     && m.MeasurementTime >= tirWindowStart)
            .Select(m => new { m.ChildId, m.GlucoseValue, m.MeasurementTime })
            .ToListAsync(cancellationToken);

        var byChild = recentMeasurements
            .GroupBy(m => m.ChildId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // TIR по каждому пациенту и среднее по группе
        var tirValues = linkedChildIds.Select(childId =>
        {
            if (!byChild.TryGetValue(childId, out var measurements) || measurements.Count == 0)
                return 0.0;

            var rangeMin = allSettings.TryGetValue(childId, out var ds)
                ? (double)ds.TargetRangeMin : GlucoseLevels.TargetRangeMin;
            var rangeMax = allSettings.TryGetValue(childId, out ds)
                ? (double)ds.TargetRangeMax : GlucoseLevels.TargetRangeMax;

            var inRange = measurements.Count(m =>
                (double)m.GlucoseValue >= rangeMin &&
                (double)m.GlucoseValue <= rangeMax);

            return (double)inRange / measurements.Count * 100.0;
        }).ToList();

        var avgTir = tirValues.Count > 0
            ? Math.Round(tirValues.Average(), 1)
            : 0.0;

        // Пациенты с критическими событиями за последние 24 часа
        var patientsWithCritical = recentMeasurements
            .Where(m => m.MeasurementTime >= todayWindowStart
                     && ((double)m.GlucoseValue < GlucoseLevels.CriticallyLowThreshold
                      || (double)m.GlucoseValue > GlucoseLevels.CriticallyHighThreshold))
            .Select(m => m.ChildId)
            .Distinct()
            .Count();

        // Пациенты без замеров за последние 24 часа
        var childrenWithMeasurementsToday = recentMeasurements
            .Where(m => m.MeasurementTime >= todayWindowStart)
            .Select(m => m.ChildId)
            .Distinct()
            .ToHashSet();

        var patientsWithoutToday = linkedChildIds
            .Count(id => !childrenWithMeasurementsToday.Contains(id));

        return new DoctorCohortSummaryDto
        {
            TotalPatients = linkedChildIds.Count,
            PatientsWithCriticalToday = patientsWithCritical,
            AverageTimeInTargetRange = avgTir,
            PatientsWithoutMeasurementsToday = patientsWithoutToday,
            GeneratedAt = DateTime.UtcNow
        };
    }
}
