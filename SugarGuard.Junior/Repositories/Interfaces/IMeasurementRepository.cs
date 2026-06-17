// Интерфейс специализированного репозитория для Measurement
// Содержит специфичные методы для измерений
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Models.Enums;

namespace SugarGuard.Junior.Repositories.Interfaces;

/// <summary>
/// Специализированный репозиторий для измерений
/// Расширяет базовый <c>IRepository&lt;Measurement&gt;</c> дополнительными методами.
/// </summary>
public interface IMeasurementRepository : IRepository<Measurement>
{
    /// <summary>
    /// Получает последнее измерение ребёнка
    /// </summary>
    Task<Measurement?> GetLatestByChildIdAsync(string childId);

    /// <summary>
    /// Получает последнее измерение ребёнка с опциональной загрузкой связанных данных
    /// </summary>
    Task<Measurement?> GetLatestByChildIdAsync(string childId, bool includeRecommendation = false, bool includeChild = false);

    /// <summary>
    /// Получает измерение по ID с опциональной загрузкой связанных данных
    /// </summary>
    Task<Measurement?> GetByIdAsync(string measurementId, bool includeRecommendation = false);

    /// <summary>
    /// Получает все измерения за дату с опциональной загрузкой связанных данных
    /// </summary>
    Task<List<Measurement>> GetByDateAsync(string childId, DateTime date, bool includeRecommendation = false, bool includeChild = false);

    /// <summary>
    /// Получает измерения за диапазон дат с постраничной загрузкой.
    /// </summary>
    Task<List<Measurement>> GetByDateRangeAsync(string childId, DateTime startDate, DateTime endDate, bool includeRecommendation = false, bool includeChild = false, int page = 1, int pageSize = 100);

    /// <summary>
    /// Получает не синхронизированные измерения
    /// </summary>
    Task<List<Measurement>> GetUnsyncedAsync();

    /// <summary>
    /// Отмечает измерение как синхронизированное
    /// </summary>
    Task<bool> MarkAsSyncedAsync(string measurementId);

    /// <summary>
    /// Удаляет все измерения ребёнка
    /// </summary>
    Task<int> DeleteAllByChildIdAsync(string childId);

    /// <summary>
    /// Получает статистику за день
    /// </summary>
    Task<MeasurementStatistics> GetDailyStatisticsAsync(string childId, DateTime date);

    /// <summary>
    /// Получает количество гипогликемических эпизодов за диапазон
    /// </summary>
    Task<int> GetHypoEpisodesAsync(string childId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Получает количество гипергликемических эпизодов за диапазон
    /// </summary>
    Task<int> GetHyperEpisodesAsync(string childId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Получает среднее значение глюкозы за диапазон
    /// </summary>
    Task<double> GetAverageGlucoseAsync(string childId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Получает процент времени в целевом диапазоне
    /// </summary>
    Task<double> GetTimeInTargetRangeAsync(string childId, DateTime startDate, DateTime endDate, double minTarget = 4.0, double maxTarget = 10.0);

    /// <summary>
    /// Получает расшифрованные заметки измерения
    /// </summary>
    Task<string?> GetDecryptedNotesAsync(Measurement measurement);

    /// <summary>
    /// Получает расшифрованное значение глюкозы
    /// </summary>
    Task<double?> GetDecryptedGlucoseValueAsync(Measurement measurement);

    /// <summary>
    /// Получает расшифрованное состояние ребёнка
    /// </summary>
    Task<ChildState?> GetDecryptedChildStateAsync(Measurement measurement);

    /// <summary>
    /// Шифрует состояние ребёнка перед сохранением
    /// </summary>
    Task<string> EncryptChildStateAsync(ChildState childState);
}

/// <summary>
/// Статистика измерений за день
/// </summary>
public class MeasurementStatistics
{
    /// <summary>
    /// Среднее значение глюкозы
    /// </summary>
    public double AverageGlucose { get; set; }

    /// <summary>
    /// Минимальное значение
    /// </summary>
    public double MinGlucose { get; set; }

    /// <summary>
    /// Максимальное значение
    /// </summary>
    public double MaxGlucose { get; set; }

    /// <summary>
    /// Стандартное отклонение (вариабельность)
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    /// Количество измерений
    /// </summary>
    public int MeasurementCount { get; set; }

    /// <summary>
    /// Процент времени в целевом диапазоне
    /// </summary>
    public double TimeInTargetRange { get; set; }

    /// <summary>
    /// Количество гипогликемических эпизодов
    /// </summary>
    public int HypoEpisodes { get; set; }

    /// <summary>
    /// Количество гипергликемических эпизодов
    /// </summary>
    public int HyperEpisodes { get; set; }

    /// <summary>
    /// Общее количество хлебных единиц за день (для аналитики)
    /// </summary>
    public double TotalBreadUnitsConsumed { get; set; }
}
