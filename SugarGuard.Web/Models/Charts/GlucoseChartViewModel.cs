namespace SugarGuard.Web.Models.Charts;

/// <summary>
/// ViewModel для передачи данных графика глюкозы
/// </summary>
public sealed class GlucoseChartViewModel
{
    public string[] Labels { get; set; } = []; // Метки оси X

    public double[] Values { get; set; } = []; // Значения глюкозы 

    public string[] PointColors { get; set; } = []; // CSS-цвета точек на графике

    public double TargetMin { get; set; } = 4.0; // Нижняя граница целевого диапазона 

    public double TargetMax { get; set; } = 10.0; // Верхняя граница целевого диапазона 

    public double WarningLowMin { get; set; } = 3.0; // Нижняя граница предупреждающего диапазона 

    public double WarningLowMax { get; set; } = 3.9; // Верхняя граница предупреждающего низкого диапазона 

    public double WarningHighMin { get; set; } = 10.1; // Нижняя граница предупреждающего высокого диапазона 

    public double WarningHighMax { get; set; } = 13.9; // Верхняя граница предупреждающего высокого диапазона

    public bool HasData => Values.Length >= 2; // Возвращает true, если данных достаточно для отрисовки графика
}
