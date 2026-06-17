using SugarGuard.Application.Glucose;
using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Infrastructure.Glucose;

public class GlucoseUiStateService : IGlucoseUiStateService
{
    public GlucoseUiState Resolve(decimal glucoseValue)
    {
        var value = (double)glucoseValue;

        if (value <= GlucoseLevels.CriticallyLowThreshold || value > GlucoseLevels.CriticallyHighThreshold)
        {
            return GlucoseUiState.Critical;
        }

        if (value >= GlucoseLevels.TargetRangeMin && value <= GlucoseLevels.TargetRangeMax)
        {
            return GlucoseUiState.Normal;
        }

        return GlucoseUiState.Attention;
    }
}
