namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на разрешение конфликта синхронизации
/// </summary>
public sealed class ResolveSyncConflictRequest
{
    public string Resolution { get; init; } = string.Empty; // Стратегия разрешения
}
