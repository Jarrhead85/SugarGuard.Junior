namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// Элемент перекуса для отображения в UI (рюкзак).
/// </summary>
public class BackpackItemViewModel
{
    public string BackpackItemId { get; set; } = string.Empty;
    public string SnackName { get; set; } = string.Empty;
    public double BreadUnits { get; set; }
    public string SnackIcon { get; set; } = "�";
}
