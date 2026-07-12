using SugarGuard.Web.Services;

namespace SugarGuard.Web.ViewModels;

/// <summary>
/// Сводка расхода токенов GigaChat для административного интерфейса.
/// </summary>
public sealed class GigaChatUsageVm
{
    /// <summary>
    /// Время построения сводки на сервере.
    /// </summary>
    public DateTime GeneratedAtUtc { get; init; }

    /// <summary>
    /// Расход за текущие сутки.
    /// </summary>
    public GigaChatUsagePeriodVm Today { get; init; } = new();

    /// <summary>
    /// Расход за текущий месяц.
    /// </summary>
    public GigaChatUsagePeriodVm Month { get; init; } = new();

    /// <summary>
    /// Расход за всё время.
    /// </summary>
    public GigaChatUsagePeriodVm AllTime { get; init; } = new();

    /// <summary>
    /// Расход токенов по детям.
    /// </summary>
    public IReadOnlyList<GigaChatChildUsageVm> Children { get; init; } = [];

    /// <summary>
    /// Месячный лимит токенов, если задан в конфигурации.
    /// </summary>
    public int? MonthlyTokenBudget { get; init; }

    /// <summary>
    /// Оставшийся месячный лимит, если бюджет задан.
    /// </summary>
    public int? MonthlyTokensRemaining { get; init; }

    /// <summary>
    /// Доля использованного месячного бюджета.
    /// </summary>
    public double MonthlyBudgetUsedPercent =>
        MonthlyTokenBudget is > 0
            ? Math.Clamp((double)Month.TotalTokens / MonthlyTokenBudget.Value * 100d, 0d, 100d)
            : 0d;

    internal static GigaChatUsageVm FromDto(GigaChatUsageDto dto) => new()
    {
        GeneratedAtUtc = dto.GeneratedAtUtc,
        Today = GigaChatUsagePeriodVm.FromDto(dto.Today),
        Month = GigaChatUsagePeriodVm.FromDto(dto.Month),
        AllTime = GigaChatUsagePeriodVm.FromDto(dto.AllTime),
        Children = dto.Children.Select(GigaChatChildUsageVm.FromDto).ToArray(),
        MonthlyTokenBudget = dto.MonthlyTokenBudget,
        MonthlyTokensRemaining = dto.MonthlyTokensRemaining
    };
}

/// <summary>
/// Расход токенов GigaChat по одному ребёнку.
/// </summary>
public sealed class GigaChatChildUsageVm
{
    /// <summary>
    /// Идентификатор ребёнка.
    /// </summary>
    public Guid ChildId { get; init; }

    /// <summary>
    /// Отображаемое имя ребёнка.
    /// </summary>
    public string ChildDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Расход за текущий месяц.
    /// </summary>
    public GigaChatUsagePeriodVm Month { get; init; } = new();

    /// <summary>
    /// Расход за всё время.
    /// </summary>
    public GigaChatUsagePeriodVm AllTime { get; init; } = new();

    internal static GigaChatChildUsageVm FromDto(GigaChatChildUsageDto dto) => new()
    {
        ChildId = dto.ChildId,
        ChildDisplayName = dto.ChildDisplayName,
        Month = GigaChatUsagePeriodVm.FromDto(dto.Month),
        AllTime = GigaChatUsagePeriodVm.FromDto(dto.AllTime)
    };
}

/// <summary>
/// Расход токенов GigaChat за один период.
/// </summary>
public sealed class GigaChatUsagePeriodVm
{
    /// <summary>
    /// Количество ответов AI с данными usage.
    /// </summary>
    public int ResponsesWithUsage { get; init; }

    /// <summary>
    /// Входные токены.
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// Выходные токены.
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Общий расход токенов.
    /// </summary>
    public int TotalTokens { get; init; }

    internal static GigaChatUsagePeriodVm FromDto(GigaChatUsagePeriodDto dto) => new()
    {
        ResponsesWithUsage = dto.ResponsesWithUsage,
        InputTokens = dto.InputTokens,
        OutputTokens = dto.OutputTokens,
        TotalTokens = dto.TotalTokens
    };
}
