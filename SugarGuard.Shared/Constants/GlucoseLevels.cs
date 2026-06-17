namespace SugarGuard.Shared.Constants;

/// <summary>
/// Централизованные константы уровней глюкозы
/// </summary>
public static class GlucoseLevels
{
    public const double InputMinValue = 1.0;
    public const double InputMaxValue = 30.0;
    public const double CriticallyLowThreshold = 3.1;
    public const double LowThreshold = 3.9;
    public const double TargetRangeMin = 4.0;
    public const double TargetRangeMax = 10.0;
    public const double HighThreshold = 10.1;
    public const double CriticallyHighThreshold = 15.0;
    public const double CacheLookupTolerance = 0.5;

    public static bool IsAttention(double glucose) =>
        (glucose >= CriticallyLowThreshold && glucose < LowThreshold) ||
        (glucose > TargetRangeMax && glucose <= CriticallyHighThreshold);

    public static bool IsCritical(double glucose) =>
        glucose < CriticallyLowThreshold || glucose > CriticallyHighThreshold;

    public static bool IsValidInput(double glucose) =>
        glucose >= InputMinValue && glucose <= InputMaxValue;

    public static string GetUiState(double glucose)
    {
        if (IsCritical(glucose))
        {
            return "Critical";
        }

        if (IsAttention(glucose))
        {
            return "Attention";
        }

        return "Normal";
    }
}
