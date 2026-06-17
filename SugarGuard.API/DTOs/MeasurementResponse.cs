namespace SugarGuard.API.DTOs;

public class MeasurementResponse
{
    public Guid MeasurementId { get; set; }
    public Guid ChildId { get; set; }
    public decimal GlucoseValue { get; set; }
    public DateTime MeasurementTime { get; set; }
    public string? ChildState { get; set; }
    public string? Notes { get; set; }
    public string? DataSource { get; set; }
    public Guid? RecommendationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string GlucoseStatus { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public string GlucoseUiState { get; set; } = string.Empty;
}
