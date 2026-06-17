using SugarGuard.Web.Models.Analytics;

namespace SugarGuard.Web.ViewModels;

/// <summary>
/// ViewModel страницы аналитики для выбранного ребёнка.
/// </summary>
public sealed class AnalyticsVm
{
    /// <summary>
    /// Идентификатор ребёнка, для которого построена аналитика.
    /// </summary>
    public Guid ChildId { get; init; }

    /// <summary>
    /// Выбранный период аналитики, например week или month.
    /// </summary>
    public string Period { get; init; } = "week";

    /// <summary>
    /// Детальная статистика по периоду.
    /// </summary>
    public StatisticsVm? Statistics { get; init; }

    /// <summary>
    /// Сравнение текущего периода с предыдущим.
    /// </summary>
    public PeriodComparisonVm? Comparison { get; init; }

    /// <summary>
    /// Выявленные паттерны глюкозы.
    /// </summary>
    public IReadOnlyList<GlucosePatternVm> Patterns { get; init; } = Array.Empty<GlucosePatternVm>();
}
