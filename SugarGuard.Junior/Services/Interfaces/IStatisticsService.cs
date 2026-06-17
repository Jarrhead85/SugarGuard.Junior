using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Сервис для расчёта статистических показателей измерений глюкозы
/// Выполняет математические расчёты на основе списка измерений
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    /// Рассчитывает полную статистику для списка измерений
    /// </summary>
    /// <param name="measurements">Список измерений для анализа</param>
    /// <param name="targetRangeMin">Минимальное значение целевого диапазона (по умолчанию 4.0)</param>
    /// <param name="targetRangeMax">Максимальное значение целевого диапазона (по умолчанию 10.0)</param>
    /// <returns>Объект со всеми статистическими показателями</returns>
    Task<MeasurementStatistics> CalculateStatisticsAsync(
        List<Measurement> measurements, 
        double targetRangeMin = 4.0, 
        double targetRangeMax = 10.0);

    /// <summary>
    /// Рассчитывает среднее значение глюкозы
    /// </summary>
    /// <param name="glucoseValues">Список значений глюкозы</param>
    /// <returns>Среднее значение с округлением до 2 знаков</returns>
    double CalculateAverage(List<double> glucoseValues);

    /// <summary>
    /// Рассчитывает стандартное отклонение (вариабельность глюкозы)
    /// </summary>
    /// <param name="glucoseValues">Список значений глюкозы</param>
    /// <returns>Стандартное отклонение с округлением до 2 знаков</returns>
    double CalculateStandardDeviation(List<double> glucoseValues);

    /// <summary>
    /// Рассчитывает процент времени в целевом диапазоне
    /// </summary>
    /// <param name="glucoseValues">Список значений глюкозы</param>
    /// <param name="minTarget">Минимальное значение диапазона</param>
    /// <param name="maxTarget">Максимальное значение диапазона</param>
    /// <returns>Процент времени в диапазоне (0-100)</returns>
    double CalculateTimeInRange(List<double> glucoseValues, double minTarget, double maxTarget);

    /// <summary>
    /// Подсчитывает количество гипогликемических эпизодов
    /// </summary>
    /// <param name="glucoseValues">Список значений глюкозы</param>
    /// <param name="threshold">Порог гипогликемии (по умолчанию 4.0)</param>
    /// <returns>Количество эпизодов ниже порога</returns>
    int CountHypoEpisodes(List<double> glucoseValues, double threshold = 4.0);

    /// <summary>
    /// Подсчитывает количество гипергликемических эпизодов
    /// </summary>
    /// <param name="glucoseValues">Список значений глюкозы</param>
    /// <param name="threshold">Порог гипергликемии (по умолчанию 10.0)</param>
    /// <returns>Количество эпизодов выше порога</returns>
    int CountHyperEpisodes(List<double> glucoseValues, double threshold = 10.0);

    /// <summary>
    /// Извлекает и дешифрует значения глюкозы из списка измерений
    /// </summary>
    /// <param name="measurements">Список измерений</param>
    /// <returns>Список дешифрованных значений глюкозы</returns>
    Task<List<double>> ExtractGlucoseValuesAsync(List<Measurement> measurements);
}