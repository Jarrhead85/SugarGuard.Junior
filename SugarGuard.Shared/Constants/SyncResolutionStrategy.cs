namespace SugarGuard.Shared.Constants;

/// <summary>
/// Имена стратегий разрешения sync-конфликтов
/// </summary>
public static class SyncResolutionStrategy
{
    public const string FirstWriteWins = "FirstWriteWins";
    public const string ServerWinsOnDuplicate = "ServerWinsOnDuplicate";
    public const string LastWriteWins = "LastWriteWins";
}
