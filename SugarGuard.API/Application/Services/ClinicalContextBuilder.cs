using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SugarGuard.API.Application.Ai;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Собирает компактный клинический контекст ребёнка для AI-консультанта.
/// </summary>
public sealed class ClinicalContextBuilder : IClinicalContextBuilder
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly AiClinicalContextOptions _options;

    /// <summary>
    /// Создаёт экземпляр построителя клинического контекста.
    /// </summary>
    public ClinicalContextBuilder(
        IDbContextFactory<AppDbContext> dbFactory,
        IOptions<AiClinicalContextOptions> options)
    {
        _dbFactory = dbFactory;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<ClinicalContext> BuildAsync(
        Guid childId,
        Guid? conversationId,
        Guid? measurementId,
        string question,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var child = await db.Children
            .AsNoTracking()
            .Include(childEntity => childEntity.DiabetesSettings)
            .FirstOrDefaultAsync(childEntity => childEntity.ChildId == childId, cancellationToken)
            ?? throw new InvalidOperationException("Ребёнок не найден при построении AI-контекста.");

        var now = DateTime.UtcNow;
        var recentFrom = now.AddHours(-_options.DetailedHistoryHours);
        var dailyFrom = now.AddHours(-24);
        var patternFrom = now.AddDays(-_options.PatternPeriodDays);

        var currentMeasurement = measurementId.HasValue
            ? await db.Measurements.AsNoTracking()
                .FirstOrDefaultAsync(measurement => measurement.ChildId == childId && measurement.MeasurementId == measurementId.Value, cancellationToken)
            : await db.Measurements.AsNoTracking()
                .Where(measurement => measurement.ChildId == childId)
                .OrderByDescending(measurement => measurement.MeasurementTime)
                .FirstOrDefaultAsync(cancellationToken);

        var recentMeasurements = await db.Measurements
            .AsNoTracking()
            .Where(measurement => measurement.ChildId == childId && measurement.MeasurementTime >= recentFrom)
            .OrderByDescending(measurement => measurement.MeasurementTime)
            .Take(_options.MaxMeasurements)
            .ToListAsync(cancellationToken);

        var recentNutrition = await db.NutritionEntries
            .AsNoTracking()
            .Where(entry => entry.ChildId == childId && entry.RecordedAt >= recentFrom)
            .OrderByDescending(entry => entry.RecordedAt)
            .Take(Math.Max(_options.MaxNutritionEvents, _options.MaxInsulinEvents))
            .ToListAsync(cancellationToken);

        var dayMeasurements = await db.Measurements
            .AsNoTracking()
            .Where(measurement => measurement.ChildId == childId && measurement.MeasurementTime >= dailyFrom)
            .ToListAsync(cancellationToken);

        var dayNutrition = await db.NutritionEntries
            .AsNoTracking()
            .Where(entry => entry.ChildId == childId && entry.RecordedAt >= dailyFrom)
            .ToListAsync(cancellationToken);

        var patternMeasurements = await db.Measurements
            .AsNoTracking()
            .Where(measurement => measurement.ChildId == childId && measurement.MeasurementTime >= patternFrom)
            .OrderBy(measurement => measurement.MeasurementTime)
            .ToListAsync(cancellationToken);

        var importantNotes = await db.DoctorNotes
            .AsNoTracking()
            .Where(note => note.ChildId == childId && note.IsImportant)
            .OrderByDescending(note => note.CreatedAt)
            .Take(3)
            .Select(note => note.NoteText)
            .ToListAsync(cancellationToken);

        AiConversation? conversation = null;
        List<AiConversationMessage> recentMessages = new();
        if (conversationId.HasValue)
        {
            conversation = await db.Set<AiConversation>()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.ChildId == childId && item.ConversationId == conversationId.Value, cancellationToken);

            recentMessages = await db.Set<AiConversationMessage>()
                .AsNoTracking()
                .Where(message => message.ConversationId == conversationId.Value)
                .OrderByDescending(message => message.CreatedAt)
                .Take(_options.MaxRecentMessages)
                .ToListAsync(cancellationToken);
        }

        var settings = child.DiabetesSettings ?? new DiabetesSettings();
        var lastMeal = recentNutrition
            .Where(entry => entry.BreadUnits > 0)
            .OrderByDescending(entry => entry.RecordedAt)
            .FirstOrDefault();
        var lastInsulin = recentNutrition
            .Where(entry => entry.InsulinUnits > 0)
            .OrderByDescending(entry => entry.RecordedAt)
            .FirstOrDefault();

        return new ClinicalContext
        {
            ChildId = childId,
            Question = Trim(question, 600),
            Profile = BuildProfile(child, settings, importantNotes),
            Current = new CurrentSituationContext
            {
                Measurement = currentMeasurement is null ? null : MapMeasurement(currentMeasurement),
                LastMeal = lastMeal is null ? null : MapNutrition(lastMeal),
                LastInsulin = lastInsulin is null ? null : MapInsulin(lastInsulin),
                MinutesSinceMeal = lastMeal is null ? null : MinutesBetween(lastMeal.RecordedAt, now),
                MinutesSinceInsulin = lastInsulin is null ? null : MinutesBetween(lastInsulin.RecordedAt, now)
            },
            RecentHistory = new RecentClinicalHistoryContext
            {
                FromUtc = recentFrom,
                Measurements = recentMeasurements
                    .OrderBy(measurement => measurement.MeasurementTime)
                    .Select(MapMeasurement)
                    .ToList(),
                Nutrition = recentNutrition
                    .Where(entry => entry.BreadUnits > 0)
                    .OrderBy(entry => entry.RecordedAt)
                    .Take(_options.MaxNutritionEvents)
                    .Select(MapNutrition)
                    .ToList(),
                Insulin = recentNutrition
                    .Where(entry => entry.InsulinUnits > 0)
                    .OrderBy(entry => entry.RecordedAt)
                    .Take(_options.MaxInsulinEvents)
                    .Select(MapInsulin)
                    .ToList()
            },
            DailySummary = BuildDailySummary(dayMeasurements, dayNutrition, settings),
            LongTermPatterns = BuildPatterns(patternMeasurements, settings),
            Conversation = new ConversationMemoryContext
            {
                ConversationId = conversation?.ConversationId,
                Summary = Trim(conversation?.Summary ?? string.Empty, _options.MaxSummaryLength),
                RecentMessages = recentMessages
                    .OrderBy(message => message.CreatedAt)
                    .Select(message => new ConversationMessageContext
                    {
                        Role = message.Role,
                        Text = Trim(message.Text, 500),
                        CreatedAt = message.CreatedAt
                    })
                    .ToList()
            }
        };
    }

    private static ClinicalProfileContext BuildProfile(Child child, DiabetesSettings settings, IReadOnlyList<string> importantNotes)
    {
        return new ClinicalProfileContext
        {
            AgeGroup = GetAgeGroup(child.DateOfBirth),
            DiabetesType = child.DiabetesType,
            TimeZoneId = child.TimeZoneId,
            TargetRangeMin = settings.TargetRangeMin,
            TargetRangeMax = settings.TargetRangeMax,
            InsulinSensitivity = settings.InsulinSensitivity,
            CarbInsulinRatio = settings.CarbInsulinRatio,
            InsulinScheme = string.IsNullOrWhiteSpace(child.InsulinScheme) ? null : Trim(child.InsulinScheme, 240),
            CurrentInsulins = Trim(child.CurrentInsulins, 500),
            ImportantDoctorNotes = importantNotes.Select(note => Trim(note, 240)).ToList()
        };
    }

    private static DailyClinicalSummaryContext BuildDailySummary(
        IReadOnlyCollection<Measurement> measurements,
        IReadOnlyCollection<NutritionEntry> nutrition,
        DiabetesSettings settings)
    {
        var values = measurements.Select(measurement => measurement.GlucoseValue).ToList();
        var inRange = values.Count == 0
            ? (decimal?)null
            : Math.Round(values.Count(value => value >= settings.TargetRangeMin && value <= settings.TargetRangeMax) * 100m / values.Count, 1);

        return new DailyClinicalSummaryContext
        {
            MeasurementCount = values.Count,
            AverageGlucose = values.Count == 0 ? null : Math.Round(values.Average(), 1),
            MinGlucose = values.Count == 0 ? null : values.Min(),
            MaxGlucose = values.Count == 0 ? null : values.Max(),
            TimeInRangePercent = inRange,
            LowEpisodes = values.Count(value => value < settings.TargetRangeMin),
            HighEpisodes = values.Count(value => value > settings.TargetRangeMax),
            TotalBreadUnits = nutrition.Sum(entry => entry.BreadUnits),
            TotalInsulinUnits = nutrition.Sum(entry => entry.InsulinUnits)
        };
    }

    private LongTermPatternsContext BuildPatterns(IReadOnlyCollection<Measurement> measurements, DiabetesSettings settings)
    {
        if (measurements.Count < 12)
        {
            return new LongTermPatternsContext
            {
                PeriodDays = _options.PatternPeriodDays,
                DataQuality = $"Недостаточно данных: {measurements.Count} измерений за {_options.PatternPeriodDays} дней",
                Observations = Array.Empty<string>()
            };
        }

        var observations = new List<string>();
        AddDayPartObservation(observations, measurements, settings, 6, 11, "утром");
        AddDayPartObservation(observations, measurements, settings, 12, 16, "днём");
        AddDayPartObservation(observations, measurements, settings, 17, 22, "вечером");

        return new LongTermPatternsContext
        {
            PeriodDays = _options.PatternPeriodDays,
            DataQuality = $"Данных достаточно для осторожных наблюдений: {measurements.Count} измерений",
            Observations = observations
        };
    }

    private static void AddDayPartObservation(
        ICollection<string> observations,
        IEnumerable<Measurement> measurements,
        DiabetesSettings settings,
        int fromHour,
        int toHour,
        string label)
    {
        var group = measurements
            .Where(measurement => measurement.MeasurementTime.Hour >= fromHour && measurement.MeasurementTime.Hour <= toHour)
            .Select(measurement => measurement.GlucoseValue)
            .ToList();

        if (group.Count < 4)
        {
            return;
        }

        var average = group.Average();
        if (average > settings.TargetRangeMax)
        {
            observations.Add($"Средняя глюкоза {label} чаще выше цели: {average:F1} ммоль/л.");
        }
        else if (average < settings.TargetRangeMin)
        {
            observations.Add($"Средняя глюкоза {label} чаще ниже цели: {average:F1} ммоль/л.");
        }
    }

    private static GlucoseContext MapMeasurement(Measurement measurement) => new()
    {
        MeasuredAt = measurement.MeasurementTime,
        Value = measurement.GlucoseValue,
        Source = measurement.DataSource ?? "unknown",
        State = measurement.ChildState
    };

    private static NutritionContext MapNutrition(NutritionEntry entry) => new()
    {
        RecordedAt = entry.RecordedAt,
        MealType = entry.MealType.ToString(),
        MealName = string.IsNullOrWhiteSpace(entry.MealName) ? null : Trim(entry.MealName, 120),
        BreadUnits = entry.BreadUnits,
        Source = entry.Source.ToString()
    };

    private static InsulinContext MapInsulin(NutritionEntry entry) => new()
    {
        RecordedAt = entry.RecordedAt,
        Units = entry.InsulinUnits,
        MealType = entry.MealType.ToString(),
        Source = entry.Source.ToString()
    };

    private static string GetAgeGroup(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age switch
        {
            < 7 => "дошкольник",
            < 12 => "младший школьник",
            < 16 => "подросток",
            _ => "старший подросток"
        };
    }

    private static int MinutesBetween(DateTime from, DateTime to) =>
        Math.Max(0, (int)(to - from).TotalMinutes);

    private static string Trim(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];
}
