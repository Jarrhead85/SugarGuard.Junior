namespace SugarGuard.Junior.Services.Interfaces;

public sealed record CgmConnectionStatus(bool IsConnected, string Provider, string? ChildId, DateTime? LastReadingAtUtc);

/// <summary>Локальная связка профиля ребёнка с Android-bridge CGM.</summary>
public interface ICgmConnectionService
{
    Task<CgmConnectionStatus> GetStatusAsync();
    Task ConnectAsync(string childId, string provider);
    Task DisconnectAsync();
    Task MarkReadingReceivedAsync(DateTime receivedAtUtc);
}
