namespace SugarGuard.Shared.Dto;

/// <summary>
/// DTO для строки таблицы измерений на дашборде
/// </summary>
public sealed class MeasurementTableItemDto
{
    public decimal GlucoseValue { get; init; } // Значение глюкозы
   
    public DateTime MeasurementTime { get; init; } // Время измерения
   
    public string? GlucoseStatus { get; init; } // Статус глюкозы
   
    public string? ChildState { get; init; } // Состояние ребёнка
   
    public string? DataSource { get; init; } // Источник данных

    public string? Notes { get; init; } // Текстовая заметка
}
