using SugarGuard.Domain.Enums;

namespace SugarGuard.Junior.Models.Core;

/// <summary>
/// Represents a single data point for the glucose chart.
/// </summary>
public record ChartDataPoint(DateTime Timestamp, decimal GlucoseValue, GlucoseUiState UiState);
