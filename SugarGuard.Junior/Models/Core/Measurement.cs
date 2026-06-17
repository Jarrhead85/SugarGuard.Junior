// Модель одного измерения глюкозы
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.Models.Core;

public class Measurement
{
    /// <summary>
    /// Уникальный ID измерения
    /// </summary>
    public string MeasurementId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID ребёнка, которому принадлежит это измерение
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Значение глюкозы в ммоль/л (допустимо 1.0-30.0)
    /// </summary>
    public string EncryptedGlucoseValue { get; set; } = string.Empty;

    /// <summary>
    /// Время измерения
    /// </summary>
    public DateTime MeasurementTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Зашифрованное состояние ребёнка (PHI)
    /// </summary>
    public string? EncryptedChildState { get; set; }

    /// <summary>
    /// Дополнительные заметки (например: "Едим пиццу", "Занимаемся спортом")
    /// Зашифрованные (PHI)
    /// </summary>
    public string? EncryptedNotes { get; set; }

    /// <summary>
    /// Источник данных (ручной ввод, глюкометр, CGM система и т.д.)
    /// </summary>
    public DataSource DataSource { get; set; } = DataSource.ManualInput;

    /// <summary>
    /// Дата и время создания записи в БД
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Был ли этот элемент синхронизирован с облаком
    /// </summary>
    public bool IsSynced { get; set; } = false;

    /// <summary>
    /// ID рекомендации ИИ, если измерение связано с запросом рекомендации
    /// </summary>
    public string? RecommendationId { get; set; }

    /// <summary>
    /// Вычисляет статус глюкозы на основе значения
    /// Используется для цветовой индикации в UI
    /// Классифицирует уровень глюкозы
    /// </summary>
    public GlucoseStatus GetStatus(double glucoseValue)
    {
        return GlucoseClassifier.Classify(glucoseValue);
    }

    /// <summary>
    /// Эмодзи для визуального представления статуса
    /// </summary>
    public string GetStatusEmoji(double glucoseValue)
    {
        return GetStatus(glucoseValue) switch
        {
            GlucoseStatus.CriticallyLow => "",      // Сирена - критическая ситуация
            GlucoseStatus.Low => "�",                 // Нейтральное лицо
            GlucoseStatus.Normal => "�",              // Улыбка - всё хорошо
            GlucoseStatus.High => "�",                // Нейтральное лицо
            GlucoseStatus.CriticallyHigh => "",      // Сирена
            _ => "❓"
        };
    }
}
