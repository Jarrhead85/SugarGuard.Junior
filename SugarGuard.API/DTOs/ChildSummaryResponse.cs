namespace SugarGuard.API.DTOs;

/// <summary>
/// Краткая информация о ребёнке в списке
/// </summary>
public sealed class ChildSummaryResponse
{
    public Guid ChildId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? DiabetesType { get; set; }
    public DateOnly? DiagnosisDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
