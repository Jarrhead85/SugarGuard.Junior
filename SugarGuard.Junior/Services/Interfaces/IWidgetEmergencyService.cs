namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>Safe background operation used by the Android home-screen widget.</summary>
public interface IWidgetEmergencyService
{
    Task<bool> SendSosAsync(CancellationToken cancellationToken = default);
}
