using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Расчёт периода и статистики по измерениям
/// </summary>
public interface IStatisticsCalculationService
{
    (DateTime fromDate, DateTime toDate, string periodName) GetPeriodRange(string period, DateTime targetDate);

    (int TotalMeasurements, double AverageGlucose, double MinGlucose, double MaxGlucose,
        double StandardDeviation, double TimeInTargetRange, int HypoEpisodes, int HyperEpisodes, int CriticalEpisodes)
    CalculateStatistics(List<Measurement> measurements);
}
