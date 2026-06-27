// Интерфейс для ИИ рекомендаций
using SugarGuard.Junior.Core.Security;
using SugarGuard.Junior.Models.Core;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Перечисление для уровня срочности рекомендации
/// </summary>
public enum RecommendationUrgency
{
    /// <summary>
    /// Обычная ситуация
    /// </summary>
    Normal,

    /// <summary>
    /// Внимание, может потребоваться действие
    /// </summary>
    Warning,

    /// <summary>
    /// Критическая ситуация, срочно нужно действие
    /// </summary>
    Critical
}

/// <summary>
/// Модель ИИ рекомендации
/// </summary>
public class AIRecommendation
{
    /// <summary>
    /// ID рекомендации
    /// </summary>
    public string RecommendationId { get; set; } = string.Empty;

    /// <summary>
    /// ID измерения, для которого сгенерирована рекомендация
    /// </summary>
    public string? MeasurementId { get; set; }

    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Значение глюкозы при запросе рекомендации
    /// </summary>
    public double GlucoseValueAtRequest { get; set; }

    /// <summary>
    /// Текст рекомендации
    /// </summary>
    public string RecommendationText { get; set; } = string.Empty;

    /// <summary>
    /// Уровень срочности
    /// </summary>
    public RecommendationUrgency Urgency { get; set; }

    /// <summary>
    /// Какая модель ИИ использовалась (для отладки)
    /// </summary>
    public string? ModelUsed { get; set; }

    /// <summary>
    /// Это рекомендация из кэша?
    /// </summary>
    public bool IsFromCache { get; set; }

    /// <summary>
    /// Время обработки (мс)
    /// </summary>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Время создания
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Версия шифрования зашифрованных полей этой записи (см. <c>EncryptionVersion</c>).
    /// Используется <see cref="VersionedEncryptionService"/> для dual-decrypt CBC/GCM при миграции.
    /// </summary>
    public EncryptionVersion EncryptionVersion { get; set; } = EncryptionVersion.AesGcm;
}

/// <summary>
/// Интерфейс для ИИ рекомендаций
/// </summary>
public interface IAIRecommendationService
{
    /// <summary>
    /// Получает рекомендацию для текущего состояния
    /// </summary>
    Task<AIRecommendation?> GetRecommendationAsync(
        string childId,
        double currentGlucose,
        List<double> recentGlucoseValues,
        string childState,
        List<string> availableSnacks,
        string? measurementId = null,
        bool forceNew = false);

    /// <summary>
    /// Получает последнюю рекомендацию
    /// </summary>
    Task<AIRecommendation?> GetLatestRecommendationAsync(string childId);

    /// <summary>
    /// Сохраняет рекомендацию
    /// </summary>
    Task<bool> SaveRecommendationAsync(AIRecommendation recommendation);

    /// <summary>
    /// Получает рекомендации за диапазон дат
    /// </summary>
    Task<List<AIRecommendation>> GetRecommendationHistoryAsync(
        string childId,
        DateTime startDate,
        DateTime endDate);
}
