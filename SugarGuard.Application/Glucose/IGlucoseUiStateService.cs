using SugarGuard.Domain.Enums;

namespace SugarGuard.Application.Glucose;

public interface IGlucoseUiStateService
{
    GlucoseUiState Resolve(decimal glucoseValue);
}
