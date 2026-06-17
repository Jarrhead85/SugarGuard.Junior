namespace SugarGuard.Web.Models.Charts;

/// <summary>
/// Одна точка данных для графика глюкозы
/// </summary>
public sealed record GlucoseChartPoint(
    string Label,
    double Value,
    string PointColor = "normal"
);
