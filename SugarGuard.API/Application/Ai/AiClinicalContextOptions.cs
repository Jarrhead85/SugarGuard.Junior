using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.Application.Ai;

/// <summary>
/// Настройки лимитов клинического контекста для AI-консультанта.
/// </summary>
public sealed class AiClinicalContextOptions
{
    /// <summary>
    /// Имя секции конфигурации.
    /// </summary>
    public const string SectionName = "AiClinicalContext";

    /// <summary>
    /// Период подробной истории в часах.
    /// </summary>
    [Range(1, 24)]
    public int DetailedHistoryHours { get; set; } = 6;

    /// <summary>
    /// Максимальное число измерений в подробной истории.
    /// </summary>
    [Range(1, 100)]
    public int MaxMeasurements { get; set; } = 24;

    /// <summary>
    /// Максимальное число событий питания в подробной истории.
    /// </summary>
    [Range(1, 100)]
    public int MaxNutritionEvents { get; set; } = 16;

    /// <summary>
    /// Максимальное число событий с инсулином в подробной истории.
    /// </summary>
    [Range(1, 100)]
    public int MaxInsulinEvents { get; set; } = 16;

    /// <summary>
    /// Период поиска долгосрочных паттернов в днях.
    /// </summary>
    [Range(7, 30)]
    public int PatternPeriodDays { get; set; } = 14;

    /// <summary>
    /// Максимальное число последних сообщений диалога.
    /// </summary>
    [Range(0, 20)]
    public int MaxRecentMessages { get; set; } = 6;

    /// <summary>
    /// Максимальная длина резюме диалога.
    /// </summary>
    [Range(200, 4000)]
    public int MaxSummaryLength { get; set; } = 1200;

    /// <summary>
    /// Максимальная длина итогового prompt в символах.
    /// </summary>
    [Range(1000, 20000)]
    public int MaxPromptCharacters { get; set; } = 8000;

    /// <summary>
    /// Через сколько сообщений обновлять краткое резюме.
    /// </summary>
    [Range(2, 50)]
    public int SummaryRefreshMessageCount { get; set; } = 8;
}
