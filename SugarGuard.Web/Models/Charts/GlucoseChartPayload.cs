namespace SugarGuard.Web.Models.Charts;

/// <summary>
/// DTO, передаваемый в JS-функцию
/// </summary>
public sealed class GlucoseChartPayload
{   
    public string[] Labels { get; set; } = []; // Метки оси X
   
    public double[] Values { get; set; } = []; // Значения глюкозы
   
    public string[] PointColors { get; set; } = []; // Цветовые маркеры точек
   
    public double TargetMin { get; set; } = 4.0; // Нижняя граница целевого диапазона
   
    public double TargetMax { get; set; } = 10.0; // Верхняя граница целевого диапазона
   
    public double WarningLowMin { get; set; } = 3.0; // Нижняя граница зоны внимания — низкий
   
    public double WarningLowMax { get; set; } = 3.9; // Верхняя граница зоны внимания — низкий
   
    public double WarningHighMin { get; set; } = 10.1; // Нижняя граница зоны внимания — высокий
    
    public double WarningHighMax { get; set; } = 13.9; // Верхняя граница зоны внимания — высокий
}
