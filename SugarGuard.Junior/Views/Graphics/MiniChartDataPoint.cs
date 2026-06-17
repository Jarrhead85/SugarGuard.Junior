using SugarGuard.Domain.Enums;

namespace SugarGuard.Junior.Views.Graphics;

/// <summary>
/// Точка данных для мини-графика на главном экране.
/// Хранит время, значение глюкозы и вычисленное UI-состояние.
/// </summary>
public readonly struct MiniChartDataPoint
{
    /// <summary>Время измерения.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Значение глюкозы в ммоль/л (double, как в MeasurementEntity).</summary>
    public double GlucoseValue { get; init; }

    /// <summary>
    /// Визуальное состояние точки: Normal / Attention / Critical.
    /// Вычисляется на стороне ViewModel по порогам DiabetesSettings.
    /// </summary>
    public GlucoseUiState UiState { get; init; }

    /// <summary>
    /// Основной конструктор — с явным UiState (предпочтительный).
    /// </summary>
    public MiniChartDataPoint(DateTime timestamp, double glucoseValue, GlucoseUiState uiState)
    {
        Timestamp = timestamp;
        GlucoseValue = glucoseValue;
        UiState = uiState;
    }

    /// <summary>
    /// Вспомогательный конструктор — без UiState (по умолчанию Normal).
    /// Используется когда состояние ещё неизвестно или несущественно.
    /// </summary>
    public MiniChartDataPoint(DateTime timestamp, double glucoseValue)
        : this(timestamp, glucoseValue, GlucoseUiState.Normal)
    {
    }
}
