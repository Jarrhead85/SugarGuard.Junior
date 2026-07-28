namespace SugarGuard.API.DTOs;

/// <summary>Сигнал работоспособности, отправляемый ботом.</summary>
public sealed class BotHeartbeatRequest
{
    public string BotName { get; init; } = string.Empty;
    public bool InternetAvailable { get; init; }
    public bool ExternalApiAvailable { get; init; }
    public string? Error { get; init; }
    public string? Version { get; init; }
}

/// <summary>Состояние внешнего бота для администратора.</summary>
public sealed class BotRuntimeStatusResponse
{
    public string BotName { get; init; } = string.Empty;
    public bool IsConfigured { get; init; }
    public bool IsOnline { get; init; }
    public bool InternetAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public DateTime? LastHeartbeatAt { get; init; }
    public DateTime? LastExternalApiSuccessAt { get; init; }
    public string? LastError { get; init; }
    public string? Version { get; init; }
    public int PendingTelegramMessages { get; init; }
    public int FailedTelegramMessages { get; init; }
}
