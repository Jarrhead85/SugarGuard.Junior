using SugarGuard.Web.Models.Analytics;
using SugarGuard.Web.Services;
using System;
using System.Collections.Generic;

namespace SugarGuard.Web.ViewModels;

/// <summary>
/// Композитная view-model для страницы родительского дашборда.
/// Объединяет данные сводки, статистики, сравнения периодов,
/// ленты событий и паттернов глюкозы.
/// </summary>
public sealed class ParentDashboardVm
{
    /// <summary>
    /// Идентификатор ребёнка, для которого собран дашборд.
    /// </summary>
    public Guid ChildId { get; init; }

    /// <summary>
    /// Краткая сводка по ребёнку:
    /// последнее измерение, счётчики критических событий,
    /// рекомендации и служебные хвостовые показатели.
    /// </summary>
    public DashboardSummaryVm Summary { get; init; } = new();

    /// <summary>
    /// Расширенная статистика за выбранный период.
    /// </summary>
    public StatisticsVm? Statistics { get; init; }

    /// <summary>
    /// Сравнение текущего периода с предыдущим аналогичной длины.
    /// </summary>
    public PeriodComparisonVm? Comparison { get; init; }

    /// <summary>
    /// Хронологическая лента событий ребёнка.
    /// </summary>
    public IReadOnlyList<TimelineEventDto> Timeline { get; init; } = Array.Empty<TimelineEventDto>();

    /// <summary>
    /// Выявленные паттерны отклонений глюкозы.
    /// </summary>
    public IReadOnlyList<GlucosePatternVm> Patterns { get; init; } = Array.Empty<GlucosePatternVm>();

    /// <summary>
    /// UTC-время формирования модели на Web-слое.
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
