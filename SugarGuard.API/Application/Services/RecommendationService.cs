using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация ИИ-рекомендаций
/// </summary>
public class RecommendationService : IRecommendationService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IGlucoseStatusService _glucoseStatusService;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IDbContextFactory<AppDbContext> dbFactory,
        IGlucoseStatusService glucoseStatusService,
        ILogger<RecommendationService> logger)
    {
        _dbFactory = dbFactory;
        _glucoseStatusService = glucoseStatusService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AIRecommendation?> FindCachedRecommendationAsync(
        Guid childId,
        decimal glucoseValue,
        CancellationToken cancellationToken = default)
    {
        const decimal tolerance = 0.5m;
        var lowerBound = glucoseValue - tolerance;
        var upperBound = glucoseValue + tolerance;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.AIRecommendations
            .AsNoTracking()
            .Where(r => r.ChildId == childId)
            .Where(r => r.GlucoseValueAtRequest >= lowerBound && r.GlucoseValueAtRequest <= upperBound)
            .Where(r => r.CreatedAt >= DateTime.UtcNow.AddHours(-24))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<Measurement>> GetRecentMeasurementsAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        var threeHoursAgo = DateTime.UtcNow.AddHours(-3);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Measurements
            .AsNoTracking()
            .Where(m => m.ChildId == childId)
            .Where(m => m.MeasurementTime >= threeHoursAgo)
            .OrderByDescending(m => m.MeasurementTime)
            .Take(10)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetAvailableSnacksAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.BackpackItems
            .AsNoTracking()
            .Where(bi => bi.ChildId == childId)
            .Select(bi => $"{bi.SnackName} ({bi.BreadUnits:F1} ХЕ)")
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public GigaChatRequest BuildGigaChatRequest(
        Child child,
        decimal glucoseValue,
        List<Measurement> recentMeasurements,
        List<string> availableSnacks)
    {
        var age = DateTime.Today.Year - child.DateOfBirth.Year;
        if (child.DateOfBirth > DateOnly.FromDateTime(DateTime.Today.AddYears(-age)))
            age--;

        var glucoseStatus = _glucoseStatusService.GetGlucoseStatus(glucoseValue);
        var recentValues = recentMeasurements
            .OrderBy(m => m.MeasurementTime)
            .Select(m => Convert.ToDouble(m.GlucoseValue))
            .ToList();
        var trend = AnalyzeTrend(recentValues);
        var settings = child.DiabetesSettings ?? new DiabetesSettings();

        return new GigaChatRequest
        {
            ChildId = child.ChildId,
            ChildAge = age,
            DiabetesType = child.DiabetesType == "Type1" ? "1 типа" : "2 типа",
            CurrentGlucose = Convert.ToDouble(glucoseValue),
            GlucoseStatus = TranslateGlucoseStatus(glucoseStatus),
            RecentGlucoseValues = recentValues,
            Trend = trend,
            TargetRangeMin = Convert.ToDouble(settings.TargetRangeMin),
            TargetRangeMax = Convert.ToDouble(settings.TargetRangeMax),
            InsulinScheme = child.InsulinScheme ?? "не указана",
            InsulinSensitivity = Convert.ToDouble(settings.InsulinSensitivity),
            AvailableSnacks = availableSnacks
        };
    }

    /// <inheritdoc/>
    public async Task<Child?> GetChildWithSettingsAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Children
            .AsNoTracking()
            .Include(c => c.DiabetesSettings)
            .FirstOrDefaultAsync(c => c.ChildId == childId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AIRecommendation> SaveRecommendationAsync(
        Guid childId,
        Guid? measurementId,
        decimal glucoseValue,
        GigaChatResponse gigaChatResponse,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var recommendation = new AIRecommendation
        {
            ChildId = childId,
            MeasurementId = measurementId,
            GlucoseValueAtRequest = glucoseValue,
            RecommendationText = gigaChatResponse.RecommendationText,
            Urgency = gigaChatResponse.Urgency,
            ModelUsed = gigaChatResponse.ModelUsed,
            IsFromCache = false,
            LatencyMs = gigaChatResponse.LatencyMs
        };

        db.AIRecommendations.Add(recommendation);
        await db.SaveChangesAsync(cancellationToken);

        if (measurementId.HasValue)
        {
            var measurement = await db.Measurements
                .FirstOrDefaultAsync(m => m.MeasurementId == measurementId.Value, cancellationToken);

            if (measurement is not null)
            {
                measurement.RecommendationId = recommendation.RecommendationId;
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "SaveRecommendationAsync: measurement {MeasurementId} не найден для привязки к рекомендации {RecommendationId}.",
                    measurementId, recommendation.RecommendationId);
            }
        }

        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Рекомендация {RecommendationId} сохранена для ребёнка {ChildId}, модель {ModelUsed}, {LatencyMs}мс.",
            recommendation.RecommendationId, childId, gigaChatResponse.ModelUsed, gigaChatResponse.LatencyMs);

        return recommendation;
    }

    /// <inheritdoc/>
    public async Task<AIRecommendation?> GetByIdAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.AIRecommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RecommendationId == recommendationId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<RecommendationHistoryPage> GetHistoryAsync(
        Guid childId,
        int limit,
        DateTime? from,
        DateTime? to,
        DateTime? cursor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var safeLimit = Math.Clamp(limit, 1, 200);

        var query = db.AIRecommendations
            .AsNoTracking()
            .Where(r => r.ChildId == childId);

        if (from.HasValue)
            query = query.Where(r => r.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.CreatedAt <= to.Value);

        if (cursor.HasValue)
        {
            query = query.Where(r => r.CreatedAt < cursor.Value);
        }

        var records = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.RecommendationId)
            .Take(safeLimit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = records.Count > safeLimit;
        var page = hasMore ? records.Take(safeLimit).ToList() : records;
        var nextCursor = hasMore && page.Count > 0
            ? page[^1].CreatedAt
            : (DateTime?)null;

        return new RecommendationHistoryPage
        {
            Items = page
                .Select(r => MapToHistoryResponse(r))
                .ToList(),
            NextCursor = nextCursor
        };
    }

    private static RecommendationResponse MapToHistoryResponse(AIRecommendation r) => new()
    {
        RecommendationId = r.RecommendationId,
        ChildId = r.ChildId,
        MeasurementId = r.MeasurementId,
        GlucoseValueAtRequest = r.GlucoseValueAtRequest,
        RecommendationText = r.RecommendationText,
        Urgency = r.Urgency,
        ModelUsed = r.ModelUsed,
        IsFromCache = false,
        LatencyMs = r.LatencyMs,
        CreatedAt = r.CreatedAt
    };

    private static string AnalyzeTrend(List<double> values)
    {
        if (values.Count < 2) return "стабильно";
        var recent = values.TakeLast(3).ToList();
        if (recent.Count < 2) return "стабильно";
        var diff = recent[^1] - recent[0];
        return diff switch { > 1.0 => "вверх", < -1.0 => "вниз", _ => "стабильно" };
    }

    private static string TranslateGlucoseStatus(string status)
    {
        return status switch
        {
            "CriticallyLow" => "КРИТИЧЕСКИ",
            "Low" => "НИЗКО",
            "Normal" => "НОРМА",
            "High" => "ВЫСОКО",
            "CriticallyHigh" => "КРИТИЧЕСКИ",
            _ => "НОРМА"
        };
    }
}
