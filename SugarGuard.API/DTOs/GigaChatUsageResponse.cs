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
    /// Расход токенов по детям без раскрытия персональных данных.
    /// </summary>
    public IReadOnlyList<GigaChatChildUsage> Children { get; init; } = [];

    /// <summary>
    /// Расход токенов по версиям системной инструкции.
    /// </summary>
    public IReadOnlyList<GigaChatPromptVersionUsage> PromptVersions { get; init; } = [];

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
    /// Число входных токенов, обслуженных GigaChat из кэша контекста.
    /// Поле <see cref="InputTokens"/> уже не включает эти токены в соответствии
    /// с семантикой usage GigaChat.
    /// </summary>
    public int PrecachedPromptTokens { get; init; }

    /// <summary>
    /// Число ответов провайдера, которые заменены локальной политикой безопасности.
    /// Метрика помогает отслеживать качество системной инструкции без показа текста диалогов.
    /// </summary>
    public int SafetyPolicyReplacements { get; init; }

    /// <summary>
    /// Сумма токенов, подлежащих тарификации по данным usage провайдера.
    /// </summary>
    public int TotalTokens { get; init; }
}

/// <summary>
/// Расход токенов GigaChat по одному ребёнку.
/// </summary>
public sealed class GigaChatChildUsage
{
    /// <summary>
    /// Идентификатор ребёнка.
    /// </summary>
    public Guid ChildId { get; init; }

    /// <summary>
    /// Безопасное отображаемое имя ребёнка для админки.
    /// </summary>
    public string ChildDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Расход за текущий месяц.
    /// </summary>
    public GigaChatUsagePeriod Month { get; init; } = new();

    /// <summary>
    /// Расход за всё время.
    /// </summary>
    public GigaChatUsagePeriod AllTime { get; init; } = new();
}

/// <summary>
/// Расход токенов для одной версии системной инструкции.
/// </summary>
public sealed class GigaChatPromptVersionUsage
{
    /// <summary>
    /// Версия инструкции. Пустое значение используется для исторических записей
    /// до появления телеметрии версий.
    /// </summary>
    public string PromptVersion { get; init; } = string.Empty;

    /// <summary>
    /// Расход за текущий месяц.
    /// </summary>
    public GigaChatUsagePeriod Month { get; init; } = new();

    /// <summary>
    /// Расход за всё время.
    /// </summary>
    public GigaChatUsagePeriod AllTime { get; init; } = new();
}
