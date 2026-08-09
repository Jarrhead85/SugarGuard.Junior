using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>Stores a local bridge/profile mapping without manufacturer credentials.</summary>
public sealed class CgmConnectionService : ICgmConnectionService
{
    private const string ConnectedKey = "cgm_bridge_connected";
    private const string ProviderKey = "cgm_bridge_provider";
    private const string ChildKey = "cgm_bridge_child_id";
    private const string LastReadingKey = "cgm_bridge_last_reading_utc";

    public Task<CgmConnectionStatus> GetStatusAsync()
    {
        var raw = Preferences.Get(LastReadingKey, string.Empty);
        DateTime? last = DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime() : null;
        return Task.FromResult(new CgmConnectionStatus(Preferences.Get(ConnectedKey, false), Preferences.Get(ProviderKey, "Juggluco"), Preferences.Get(ChildKey, string.Empty), last));
    }

    public Task ConnectAsync(string childId, string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childId);
        Preferences.Set(ConnectedKey, true);
        Preferences.Set(ProviderKey, string.IsNullOrWhiteSpace(provider) ? "Juggluco" : provider);
        Preferences.Set(ChildKey, childId);
        Preferences.Remove(LastReadingKey);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        Preferences.Set(ConnectedKey, false);
        Preferences.Remove(ChildKey);
        Preferences.Remove(LastReadingKey);
        return Task.CompletedTask;
    }

    public Task MarkReadingReceivedAsync(DateTime receivedAtUtc)
    {
        Preferences.Set(LastReadingKey, receivedAtUtc.ToUniversalTime().ToString("O"));
        return Task.CompletedTask;
    }
}
