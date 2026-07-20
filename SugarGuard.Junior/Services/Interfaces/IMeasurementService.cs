using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Models.Enums;

using SugarGuard.Junior.Core.Sensors;
using SugarGuard.Junior.Models.Sensors;

namespace SugarGuard.Junior.Services.Interfaces;

// Сервис для работы с измерениями глюкозы
public interface IMeasurementService
{
    /// <summary>
    /// Получает последние два измерения ребёнка
    /// Используется на Главной странице для отображения тренда
    /// </summary>
    Task<List<MeasurementEntity>> GetLastTwoMeasurementsAsync(string childId);

    /// <summary>
    /// Обрабатывает новое измерение и получает рекомендацию
    /// 1. Сохраняет измерение в БД
    /// 2. Определяет статус (норма/гипо/гипер)
    /// 3. Возвращает рекомендацию
    /// </summary>
    Task<RecommendationResponse?> ProcessMeasurementWithRecommendationAsync(
        string childId,
        double glucoseValue,
        string childState = "normal",
        DateTime? measurementTimeUtc = null);

    /// <summary>
    /// Сохраняет показание внешнего датчика без запроса к ИИ.
    /// Метод идемпотентен для повторно доставленного broadcast и работает офлайн.
    /// </summary>
    Task<SensorMeasurementSaveResult> ProcessSensorMeasurementAsync(
        string childId,
        SensorGlucoseReading reading);

    /// <summary>
    /// Получает количество измерений за сегодня
    /// </summary>
    Task<int> GetMeasurementCountTodayAsync(string childId);

    /// <summary>
    /// Определяет, являются ли данные критическими
    /// Критично: &lt; 3.3 или &gt; 15.0 ммоль/л
    /// </summary>
    /// <param name="glucoseValue">Значение глюкозы в ммоль/л</param>
    /// <returns>true если уровень критический, иначе false</returns>
    bool IsCritical(double glucoseValue);

    /// <summary>
    /// Определяет статус глюкозы по значению
    /// </summary>
    GlucoseStatus GetStatus(double glucoseValue);

    /// <summary>
    /// Логирует съеденный перекус
    /// Перемещает перекус из рюкзака в историю
    /// </summary>
    Task<bool> LogSnackConsumedAsync(SnackConsumedRequest request);

    /// <summary>
    /// Логирует пропущенную рекомендацию
    /// </summary>
    Task<bool> LogSkippedRecommendationAsync(SkippedRecommendationRequest request);
}
