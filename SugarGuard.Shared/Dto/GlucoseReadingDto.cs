namespace SugarGuard.Shared.Dto;

/// <summary>
/// DTO одного показания глюкозы для передачи из API в Web-клиент и отображения на графике
/// </summary>

public sealed record GlucoseReadingDto(
    Guid MeasurementId,
    decimal Value,
    DateTime RecordedAt,
    string? ChildState,
    string? DataSource,
    string? Notes
);
