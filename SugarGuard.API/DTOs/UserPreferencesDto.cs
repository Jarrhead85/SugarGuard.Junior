namespace SugarGuard.API.DTOs;

public sealed class UserPreferencesDto
{
    public bool AlertsCritical { get; init; } = true;
    public bool AlertsDailySummary { get; init; } = true;
    public bool AlertsMissedMeasurement { get; init; } = false;
}

public sealed class SaveUserPreferencesRequest
{
    public bool AlertsCritical { get; init; } = true;
    public bool AlertsDailySummary { get; init; } = true;
    public bool AlertsMissedMeasurement { get; init; } = false;
}
