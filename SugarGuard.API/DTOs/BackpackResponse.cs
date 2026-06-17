namespace SugarGuard.API.DTOs;

/// <summary>
/// DTO для ответа с полным содержимым рюкзака ребёнка
/// </summary>
public class BackpackResponse
{
    public Guid ChildId { get; set; }
    public List<BackpackItemResponse> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public decimal TotalBreadUnits { get; set; }
    public DateTime LastUpdated { get; set; }
}
