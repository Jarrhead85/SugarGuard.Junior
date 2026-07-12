namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// Элемент перекуса для отображения в UI (рюкзак).
/// </summary>
public class BackpackItemViewModel
{
    public string BackpackItemId { get; set; } = string.Empty;
    public List<string> BackpackItemIds { get; set; } = [];
    public string SnackName { get; set; } = string.Empty;
    public double BreadUnits { get; set; }
    public int Quantity { get; set; } = 1;
    public string SnackIconSource { get; set; } = "snack_generic.svg";

    public string QuantityText => Quantity == 1 ? "1 шт." : $"{Quantity} шт.";

    public double TotalBreadUnits => BreadUnits * Quantity;
}
