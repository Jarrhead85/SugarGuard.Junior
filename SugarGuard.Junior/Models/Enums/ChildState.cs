// Состояние ребёнка в момент измерения
// Влияет на рекомендации ИИ
namespace SugarGuard.Junior.Models.Enums;

public enum ChildState
{
    /// <summary>
    /// Состояние неизвестно или не указано
    /// </summary>
    Unknown,

    /// <summary>
    /// Нормальное состояние
    /// </summary>
    Normal,

    /// <summary>
    /// Только проснулся
    /// </summary>
    WakeUp,

    /// <summary>
    /// До приёма пищи (натощак)
    /// </summary>
    BeforeMeal,

    /// <summary>
    /// После приёма пищи
    /// </summary>
    AfterMeal,

    /// <summary>
    /// Во время спорта/физической активности
    /// </summary>
    Physical,

    /// <summary>
    /// Перед сном
    /// </summary>
    BeforeSleep,

    /// <summary>
    /// Ночное время (00:00-06:00)
    /// </summary>
    Night,

    /// <summary>
    /// Гипогликемия (низкий сахар)
    /// </summary>
    Hypoglycemia,

    /// <summary>
    /// Гипергликемия (высокий сахар)
    /// </summary>
    Hyperglycemia
}
