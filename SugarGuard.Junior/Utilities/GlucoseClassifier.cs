using SugarGuard.Junior.Models.Enums;
using SharedGlucoseLevels = SugarGuard.Shared.Constants.GlucoseLevels;

namespace SugarGuard.Junior.Utilities;

public static class GlucoseClassifier
{
    public static GlucoseStatus Classify(double glucose) => glucose switch
    {
        < SharedGlucoseLevels.CriticallyLowThreshold => GlucoseStatus.CriticallyLow,
        < SharedGlucoseLevels.LowThreshold => GlucoseStatus.Low,
        <= SharedGlucoseLevels.TargetRangeMax => GlucoseStatus.Normal,
        <= SharedGlucoseLevels.CriticallyHighThreshold => GlucoseStatus.High,
        _ => GlucoseStatus.CriticallyHigh
    };
}
