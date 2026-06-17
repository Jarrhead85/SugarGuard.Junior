namespace SugarGuard.API.DTOs;

/// <summary>
/// DTO для ответа с данными перекуса из рюкзака
/// </summary>
public class BackpackItemResponse
{
    public Guid BackpackItemId { get; set; }
    public Guid ChildId { get; set; }
    public string SnackName { get; set; } = string.Empty;
    public decimal BreadUnits { get; set; }
    public string? AddedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
