using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Ai;

/// <summary>
/// Структурированный клинический контекст для одного AI-запроса.
/// </summary>
public sealed class ClinicalContext
{
    /// <summary>
    /// Версия формата контекста.
    /// </summary>
    public string FormatVersion { get; set; } = "ai-context-v1";

    /// <summary>
    /// Идентификатор ребёнка внутри SugarGuard.
    /// </summary>
    public Guid ChildId { get; set; }

    /// <summary>
    /// Постоянный профиль ребёнка без ФИО и контактов.
    /// </summary>
    public ClinicalProfileContext Profile { get; set; } = new();

    /// <summary>
    /// Текущая ситуация вокруг запроса.
    /// </summary>
    public CurrentSituationContext Current { get; set; } = new();

    /// <summary>
    /// Active snacks currently available in the child's backpack.
    /// </summary>
    public IReadOnlyList<BackpackSnackContext> AvailableBackpack { get; set; } = Array.Empty<BackpackSnackContext>();

    /// <summary>
    /// Подробная недавняя история.
    /// </summary>
    public RecentClinicalHistoryContext RecentHistory { get; set; } = new();

    /// <summary>
    /// Суточная сводка.
    /// </summary>
    public DailyClinicalSummaryContext DailySummary { get; set; } = new();

    /// <summary>
    /// Долгосрочные паттерны с оценкой качества данных.
    /// </summary>
    public LongTermPatternsContext LongTermPatterns { get; set; } = new();

    /// <summary>
    /// Контекст текущей беседы.
    /// </summary>
    public ConversationMemoryContext Conversation { get; set; } = new();

    /// <summary>
    /// Вопрос пользователя без лишних персональных данных.
    /// </summary>
    public string Question { get; set; } = string.Empty;
}

/// <summary>
/// Постоянный профиль ребёнка без персональных контактов.
/// </summary>
public sealed class ClinicalProfileContext
{
    /// <summary>
    /// Возрастная группа.
    /// </summary>
    public string AgeGroup { get; set; } = string.Empty;

    /// <summary>
    /// Тип диабета.
    /// </summary>
    public string DiabetesType { get; set; } = string.Empty;

    /// <summary>
    /// Часовой пояс ребёнка.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>
    /// Нижняя граница целевого диапазона.
    /// </summary>
    public decimal TargetRangeMin { get; set; }

    /// <summary>
    /// Верхняя граница целевого диапазона.
    /// </summary>
    public decimal TargetRangeMax { get; set; }

    /// <summary>
    /// Коэффициент чувствительности к инсулину.
    /// </summary>
    public decimal InsulinSensitivity { get; set; }

    /// <summary>
    /// Углеводный коэффициент.
    /// </summary>
    public decimal CarbInsulinRatio { get; set; }

    /// <summary>
    /// Текущая схема инсулинотерапии, если она указана.
    /// </summary>
    public string? InsulinScheme { get; set; }

    /// <summary>
    /// Текущие препараты инсулина в обезличенном виде.
    /// </summary>
    public string CurrentInsulins { get; set; } = "[]";

    /// <summary>
    /// Последние важные врачебные заметки.
    /// </summary>
    public IReadOnlyList<string> ImportantDoctorNotes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Текущая ситуация вокруг запроса.
/// </summary>
public sealed class CurrentSituationContext
{
    /// <summary>
    /// Новое или текущее измерение.
    /// </summary>
    public GlucoseContext? Measurement { get; set; }

    /// <summary>
    /// Последний приём пищи.
    /// </summary>
    public NutritionContext? LastMeal { get; set; }

    /// <summary>
    /// Последнее событие с инсулином.
    /// </summary>
    public InsulinContext? LastInsulin { get; set; }

    /// <summary>
    /// Минут с последнего приёма пищи.
    /// </summary>
    public int? MinutesSinceMeal { get; set; }

    /// <summary>
    /// Минут с последнего введения инсулина.
    /// </summary>
    public int? MinutesSinceInsulin { get; set; }
}

/// <summary>
/// Измерение глюкозы в AI-контексте.
/// </summary>
public sealed class GlucoseContext
{
    /// <summary>
    /// UTC-время измерения.
    /// </summary>
    public DateTime MeasuredAt { get; set; }

    /// <summary>
    /// Значение глюкозы.
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Источник записи.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Состояние ребёнка, если оно указано.
    /// </summary>
    public string? State { get; set; }
}

/// <summary>
/// Событие питания в AI-контексте.
/// </summary>
public sealed class NutritionContext
{
    /// <summary>
    /// UTC-время события.
    /// </summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Тип приёма пищи.
    /// </summary>
    public string MealType { get; set; } = string.Empty;

    /// <summary>
    /// Название еды, если оно хранится.
    /// </summary>
    public string? MealName { get; set; }

    /// <summary>
    /// Количество хлебных единиц.
    /// </summary>
    public decimal BreadUnits { get; set; }

    /// <summary>
    /// Источник записи.
    /// </summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Событие введения инсулина в AI-контексте.
/// </summary>
public sealed class InsulinContext
{
    /// <summary>
    /// UTC-время события.
    /// </summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Количество единиц инсулина.
    /// </summary>
    public decimal Units { get; set; }

    /// <summary>
    /// Тип приёма пищи, с которым связана запись.
    /// </summary>
    public string MealType { get; set; } = string.Empty;

    /// <summary>
    /// Источник записи.
    /// </summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Backpack snack included in the AI context.
/// </summary>
public sealed class BackpackSnackContext
{
    public string SnackName { get; set; } = string.Empty;

    public decimal BreadUnits { get; set; }

    public DateTime RecordedAt { get; set; }
}

/// <summary>
/// Подробная недавняя история для AI-контекста.
/// </summary>
public sealed class RecentClinicalHistoryContext
{
    /// <summary>
    /// Начало периода.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Измерения глюкозы.
    /// </summary>
    public IReadOnlyList<GlucoseContext> Measurements { get; set; } = Array.Empty<GlucoseContext>();

    /// <summary>
    /// События питания.
    /// </summary>
    public IReadOnlyList<NutritionContext> Nutrition { get; set; } = Array.Empty<NutritionContext>();

    /// <summary>
    /// События с инсулином.
    /// </summary>
    public IReadOnlyList<InsulinContext> Insulin { get; set; } = Array.Empty<InsulinContext>();

    /// <summary>
    /// Recently consumed backpack snacks.
    /// </summary>
    public IReadOnlyList<BackpackSnackContext> ConsumedBackpackSnacks { get; set; } = Array.Empty<BackpackSnackContext>();
}

/// <summary>
/// Суточные агрегаты для AI-контекста.
/// </summary>
public sealed class DailyClinicalSummaryContext
{
    /// <summary>
    /// Число измерений.
    /// </summary>
    public int MeasurementCount { get; set; }

    /// <summary>
    /// Средняя глюкоза.
    /// </summary>
    public decimal? AverageGlucose { get; set; }

    /// <summary>
    /// Минимальная глюкоза.
    /// </summary>
    public decimal? MinGlucose { get; set; }

    /// <summary>
    /// Максимальная глюкоза.
    /// </summary>
    public decimal? MaxGlucose { get; set; }

    /// <summary>
    /// Процент времени в целевом диапазоне.
    /// </summary>
    public decimal? TimeInRangePercent { get; set; }

    /// <summary>
    /// Число эпизодов ниже диапазона.
    /// </summary>
    public int LowEpisodes { get; set; }

    /// <summary>
    /// Число эпизодов выше диапазона.
    /// </summary>
    public int HighEpisodes { get; set; }

    /// <summary>
    /// Суммарные хлебные единицы за сутки.
    /// </summary>
    public decimal TotalBreadUnits { get; set; }

    /// <summary>
    /// Суммарный фактически записанный инсулин за сутки.
    /// </summary>
    public decimal TotalInsulinUnits { get; set; }
}

/// <summary>
/// Долгосрочные паттерны для AI-контекста.
/// </summary>
public sealed class LongTermPatternsContext
{
    /// <summary>
    /// Период анализа в днях.
    /// </summary>
    public int PeriodDays { get; set; }

    /// <summary>
    /// Качество данных.
    /// </summary>
    public string DataQuality { get; set; } = "Недостаточно данных";

    /// <summary>
    /// Вычисленные наблюдения без медицинских назначений.
    /// </summary>
    public IReadOnlyList<string> Observations { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Память текущей AI-беседы.
/// </summary>
public sealed class ConversationMemoryContext
{
    /// <summary>
    /// Идентификатор активной конверсации.
    /// </summary>
    public Guid? ConversationId { get; set; }

    /// <summary>
    /// Краткое резюме беседы.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Последние сообщения для связности ответа.
    /// </summary>
    public IReadOnlyList<ConversationMessageContext> RecentMessages { get; set; } = Array.Empty<ConversationMessageContext>();
}

/// <summary>
/// Последнее сообщение AI-беседы в обезличенном контексте.
/// </summary>
public sealed class ConversationMessageContext
{
    /// <summary>
    /// Роль сообщения.
    /// </summary>
    public AiMessageRole Role { get; set; }

    /// <summary>
    /// Текст сообщения.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// UTC-время создания.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
