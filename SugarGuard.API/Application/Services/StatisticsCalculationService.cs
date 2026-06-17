using SugarGuard.API.Application.Interfaces;
using SugarGuard.Domain.Entities;
using SugarGuard.Application.Glucose;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Расчёт временных периодов и статистики по измерениям глюкозы
/// </summary>
public sealed class StatisticsCalculationService : IStatisticsCalculationService
{
    private readonly ILogger<StatisticsCalculationService> _logger;

    /// <summary>
    /// Конструктор
    /// </summary>
    public StatisticsCalculationService(ILogger<StatisticsCalculationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Возвращает границы периода (UTC) и его локализованное название
    /// </summary>
    public (DateTime fromDate, DateTime toDate, string periodName) GetPeriodRange(
        string period,
        DateTime targetDate)
    {
        return period.ToLowerInvariant() switch
        {
            "day" => (
                targetDate.Date,
                targetDate.Date.AddDays(1).AddTicks(-1),
                "День"
            ),

            // Неделя начинается с понедельника
            "week" => (
                targetDate.Date.AddDays(-(((int)targetDate.DayOfWeek + 6) % 7)),
                targetDate.Date.AddDays(7 - ((int)targetDate.DayOfWeek + 6) % 7).AddTicks(-1),
                "Неделя"
            ),

            "month" => (
                new DateTime(targetDate.Year, targetDate.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(targetDate.Year, targetDate.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddMonths(1).AddTicks(-1),
                "Месяц"
            ),

            "year" => (
                new DateTime(targetDate.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(targetDate.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1),
                "Год"
            ),

            _ => (
                targetDate.Date,
                targetDate.Date.AddDays(1).AddTicks(-1),
                "День"
            )
        };
    }

    /// <summary>
    /// Вычисляет агрегированную статистику по списку измерений
    /// </summary>
    public (int TotalMeasurements, double AverageGlucose, double MinGlucose, double MaxGlucose,
        double StandardDeviation, double TimeInTargetRange, int HypoEpisodes, int HyperEpisodes,
        int CriticalEpisodes)
    CalculateStatistics(List<Measurement> measurements)
    {
        if (measurements is null || measurements.Count == 0)
        {
            _logger.LogDebug("CalculateStatistics: получен пустой список измерений — возвращаем нули.");
            return (0, 0.0, 0.0, 0.0, 0.0, 0.0, 0, 0, 0);
        }

        var values = measurements.Select(m => (double)m.GlucoseValue).ToList();
        var count = values.Count;

        var average = values.Average();
        var min = values.Min();
        var max = values.Max();
        var variance = values.Sum(x => Math.Pow(x - average, 2)) / count;
        var stdDev = Math.Sqrt(variance);

        // Пороги из единого источника правды
        var targetMin = GlucoseLevels.TargetRangeMin;
        var targetMax = GlucoseLevels.TargetRangeMax;
        var critLow = GlucoseLevels.CriticallyLowThreshold;
        var critHigh = GlucoseLevels.CriticallyHighThreshold;
        var hypoThresh = GlucoseLevels.LowThreshold;
        var hyperThresh = GlucoseLevels.HighThreshold;

        var inRange = values.Count(g => g >= targetMin && g <= targetMax);
        var timeInRange = inRange / (double)count * 100.0;
        var hypoEpisodes = values.Count(g => g < hypoThresh);
        var hyperEpisodes = values.Count(g => g > hyperThresh);
        var critEpisodes = values.Count(g => g < critLow || g > critHigh);

        _logger.LogDebug(
            "CalculateStatistics: Count={Count}, Avg={Avg:F2}, TIR={Tir:F1}%, Hypo={Hypo}, Hyper={Hyper}, Crit={Crit}.",
            count, average, timeInRange, hypoEpisodes, hyperEpisodes, critEpisodes);

        return (
            count,
            Math.Round(average, 2),
            Math.Round(min, 2),
            Math.Round(max, 2),
            Math.Round(stdDev, 2),
            Math.Round(timeInRange, 2),
            hypoEpisodes,
            hyperEpisodes,
            critEpisodes
        );
    }
}
