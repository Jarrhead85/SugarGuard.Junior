using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Поддерживает актуальность уже отправленной ежедневной сводки, когда ребёнок
/// присылает новые данные после планового задания в 21:00.
/// </summary>
public sealed class DailyParentSummaryRefreshService : IDailyParentSummaryRefreshService
{
    private const string SourceType = "daily_parent_summary";
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public DailyParentSummaryRefreshService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task RefreshCurrentDayAsync(Guid childId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var nowInMoscow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MoscowTimeZone);
        var dayStart = ToUtc(DateOnly.FromDateTime(nowInMoscow));
        var dayEnd = dayStart.AddDays(1);

        var notifications = await db.UserNotifications
            .Where(item => item.ChildId == childId
                           && item.SourceType == SourceType
                           && item.CreatedAt >= dayStart
                           && item.CreatedAt < dayEnd)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return;
        }

        var settings = await db.DiabetesSettings
            .AsNoTracking()
            .Where(item => item.ChildId == childId)
            .Select(item => new { item.TargetRangeMin, item.TargetRangeMax })
            .FirstOrDefaultAsync(cancellationToken);
        var targetMin = settings?.TargetRangeMin ?? 4.0m;
        var targetMax = settings?.TargetRangeMax ?? 10.0m;

        var measurements = await db.Measurements
            .AsNoTracking()
            .Where(item => item.ChildId == childId && item.MeasurementTime >= dayStart && item.MeasurementTime < dayEnd)
            .Select(item => item.GlucoseValue)
            .ToListAsync(cancellationToken);
        var nutrition = await db.NutritionEntries
            .AsNoTracking()
            .Where(item => item.ChildId == childId && item.RecordedAt >= dayStart && item.RecordedAt < dayEnd)
            .GroupBy(_ => 1)
            .Select(group => new { BreadUnits = group.Sum(item => item.BreadUnits), InsulinUnits = group.Sum(item => item.InsulinUnits) })
            .FirstOrDefaultAsync(cancellationToken);

        var glucoseLine = measurements.Count == 0
            ? "Измерений глюкозы сегодня не было."
            : $"Глюкоза: {measurements.Count} изм., средняя {measurements.Average():0.0} ммоль/л, диапазон {measurements.Min():0.0}–{measurements.Max():0.0} ммоль/л, в цели {measurements.Count(value => value >= targetMin && value <= targetMax) * 100 / measurements.Count}%.";
        var nutritionLine = $"Питание: {nutrition?.BreadUnits ?? 0m:0.##} ХЕ, инсулин {nutrition?.InsulinUnits ?? 0m:0.##} ед.";
        var description = $"{glucoseLine} {nutritionLine}";

        foreach (var notification in notifications)
        {
            notification.Description = description;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static DateTime ToUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), MoscowTimeZone);

    private static readonly TimeZoneInfo MoscowTimeZone = ResolveMoscowTimeZone();

    private static TimeZoneInfo ResolveMoscowTimeZone()
    {
        foreach (var id in new[] { "Europe/Moscow", "Russian Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Utc;
    }
}
