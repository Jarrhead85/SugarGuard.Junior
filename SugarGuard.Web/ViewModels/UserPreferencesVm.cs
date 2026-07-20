namespace SugarGuard.Web.ViewModels;

public sealed class UserPreferencesVm
{
    public bool AlertsCritical { get; init; } = true;
    public bool AlertsDailySummary { get; init; } = true;
    public bool AlertsMissedMeasurement { get; init; } = false;
    public string MapProvider { get; init; } = "yandex";
}
