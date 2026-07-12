namespace SugarGuard.API.DTOs;

/// <summary>
/// Сводка расхода токенов GigaChat для администратора.
/// </summary>
public sealed class GigaChatUsageResponse
{
    /// <summary>
    /// UTC-время построения сводки.
    /// </summary>
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Расход за текущие сутки UTC.
    /// </summary>
    public GigaChatUsagePeriod Today { get; init; } = new();

    /// <summary>
    /// Расход за текущий месяц UTC.
    /// </summary>
    public GigaChatUsagePeriod Month { get; init; } = new();

    /// <summary>
    /// Расход за всё время.
    /// </summary>
    public GigaChatUsagePeriod AllTime { get; init; } = new();

    /// <summary>
    /// Месячный лимит токенов из конфигурации, если задан.
    /// </summary>
    public int? MonthlyTokenBudget { get; init; }

    /// <summary>
    /// Оставшиеся токены в месячном лимите, если лимит задан.
    /// </summary>
    public int? MonthlyTokensRemaining { get; init; }
}

/// <summary>
/// Расход токенов за период.
/// </summary>
public sealed class GigaChatUsagePeriod
{
    /// <summary>
    /// Число AI-ответов с usage.
    /// </summary>
    public int ResponsesWithUsage { get; init; }

    /// <summary>
    /// Сумма входных токенов.
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// Сумма выходных токенов.
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Сумма всех токенов.
    /// </summary>
    public int TotalTokens { get; init; }
}
