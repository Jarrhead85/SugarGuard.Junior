using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для расчёта статистических показателей измерений глюкозы
/// Реализует математические расчёты: среднее, мин, макс, время в диапазоне, 
/// гипо/гипер эпизоды, стандартное отклонение
/// </summary>
public class StatisticsService : IStatisticsService
{
    private readonly ICryptoService _cryptoService;
    private readonly ILogger<StatisticsService> _logger;

    public StatisticsService(
        ICryptoService cryptoService,
        ILogger<StatisticsService> logger)
    {
        _cryptoService = cryptoService;
        _logger = logger;
    }

    /// <summary>
    /// Рассчитывает полную статистику для списка измерений
    /// </summary>
    public async Task<MeasurementStatistics> CalculateStatisticsAsync(
        List<Measurement> measurements, 
        double targetRangeMin = 4.0, 
        double targetRangeMax = 10.0)
    {
        try
        {
            if (measurements == null || measurements.Count == 0)
            {
                _logger.LogDebug("Пустой список измерений - возвращаем пустую статистику");
                return new MeasurementStatistics();
            }

            // Извлекаем и дешифруем значения глюкозы
            var glucoseValues = await ExtractGlucoseValuesAsync(measurements);

            if (glucoseValues.Count == 0)
            {
                _logger.LogWarning("Не удалось извлечь ни одного значения глюкозы");
                return new MeasurementStatistics { MeasurementCount = measurements.Count };
            }

            // Рассчитываем все показатели
            var average = CalculateAverage(glucoseValues);
            var min = glucoseValues.Min();
            var max = glucoseValues.Max();
            var standardDeviation = CalculateStandardDeviation(glucoseValues);
            var timeInRange = CalculateTimeInRange(glucoseValues, targetRangeMin, targetRangeMax);
            var hypoEpisodes = CountHypoEpisodes(glucoseValues);
            var hyperEpisodes = CountHyperEpisodes(glucoseValues);

            var statistics = new MeasurementStatistics
            {
                AverageGlucose = Math.Round(average, 2),
                MinGlucose = Math.Round(min, 2),
                MaxGlucose = Math.Round(max, 2),
                StandardDeviation = Math.Round(standardDeviation, 2),
                MeasurementCount = glucoseValues.Count,
                TimeInTargetRange = Math.Round(timeInRange, 2),
                HypoEpisodes = hypoEpisodes,
                HyperEpisodes = hyperEpisodes
            };

            _logger.LogInformation(" Статистика рассчитана: {MeasurementCount} измерений, среднее {AverageGlucose}, в норме {TimeInTargetRange}%",
                statistics.MeasurementCount, statistics.AverageGlucose, statistics.TimeInTargetRange);

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при расчёте статистики: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Рассчитывает среднее значение глюкозы
    /// </summary>
    public double CalculateAverage(List<double> glucoseValues)
    {
        if (glucoseValues == null || glucoseValues.Count == 0)
            return 0;

        return glucoseValues.Average();
    }

    /// <summary>
    /// Рассчитывает стандартное отклонение (вариабельность глюкозы)
    /// Формула: sqrt(sum((x - mean)²) / n)
    /// </summary>
    public double CalculateStandardDeviation(List<double> glucoseValues)
    {
        if (glucoseValues == null || glucoseValues.Count == 0)
            return 0;

        if (glucoseValues.Count == 1)
            return 0; // Стандартное отклонение для одного значения равно 0

        var average = glucoseValues.Average();
        var variance = glucoseValues.Sum(x => Math.Pow(x - average, 2)) / glucoseValues.Count;
        return Math.Sqrt(variance);
    }

    /// <summary>
    /// Рассчитывает процент времени в целевом диапазоне
    /// </summary>
    public double CalculateTimeInRange(List<double> glucoseValues, double minTarget, double maxTarget)
    {
        if (glucoseValues == null || glucoseValues.Count == 0)
            return 0;

        var inRange = glucoseValues.Count(g => g >= minTarget && g <= maxTarget);
        return (inRange / (double)glucoseValues.Count) * 100;
    }

    /// <summary>
    /// Подсчитывает количество гипогликемических эпизодов
    /// </summary>
    public int CountHypoEpisodes(List<double> glucoseValues, double threshold = 4.0)
    {
        if (glucoseValues == null || glucoseValues.Count == 0)
            return 0;

        return glucoseValues.Count(g => g < threshold);
    }

    /// <summary>
    /// Подсчитывает количество гипергликемических эпизодов
    /// </summary>
    public int CountHyperEpisodes(List<double> glucoseValues, double threshold = 10.0)
    {
        if (glucoseValues == null || glucoseValues.Count == 0)
            return 0;

        return glucoseValues.Count(g => g > threshold);
    }

    /// <summary>
    /// Извлекает и дешифрует значения глюкозы из списка измерений.
    /// Расшифровка выполняется параллельно через <see cref="Task.WhenAll{TResult}(IEnumerable{Task{TResult}})"/>:
    /// 100 измерений × ~50 мс Keychain = 100 мс вместо 5000 мс.
    /// </summary>
    public async Task<List<double>> ExtractGlucoseValuesAsync(List<Measurement> measurements)
    {
        var glucoseValues = new List<double>();

        if (measurements == null || measurements.Count == 0)
            return glucoseValues;

        // Параллельная расшифровка — Keychain/Keystore операции async-IO,
        // и Task.WhenAll реально ускоряет батч в N раз.
        var decryptTasks = measurements
            .Select(m => SafeDecryptAsync(m.EncryptedGlucoseValue))
            .ToList();

        var decryptedValues = await Task.WhenAll(decryptTasks);

        for (int i = 0; i < decryptedValues.Length; i++)
        {
            var decryptedValue = decryptedValues[i];
            if (string.IsNullOrEmpty(decryptedValue))
                continue;

            if (DoubleParser.TryParseDecrypted(decryptedValue, out var glucoseValue))
            {
                if (glucoseValue >= 1.0 && glucoseValue <= 30.0)
                {
                    glucoseValues.Add(glucoseValue);
                }
                else
                {
                    _logger.LogWarning("Значение глюкозы вне диапазона: {GlucoseValue}", glucoseValue);
                }
            }
            else
            {
                _logger.LogWarning("Не удалось распарсить значение глюкозы: {DecryptedValue}", decryptedValue);
            }
        }

        _logger.LogDebug("Извлечено {GlucoseCount} из {MeasurementCount} значений глюкозы", glucoseValues.Count, measurements.Count);
        return glucoseValues;
    }

    /// <summary>
    /// Безопасная обёртка расшифровки: ловит исключения и возвращает null,
    /// чтобы один битый ciphertext не ломал весь Task.WhenAll.
    /// </summary>
    private async Task<string?> SafeDecryptAsync(string encryptedValue)
    {
        try
        {
            return await _cryptoService.DecryptAsync(encryptedValue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Не удалось дешифровать значение глюкозы: {ErrorMessage}", ex.Message);
            return null;
        }
    }
}
