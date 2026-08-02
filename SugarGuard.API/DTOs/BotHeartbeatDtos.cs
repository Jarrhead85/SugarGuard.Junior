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

/// <summary>
/// Безопасный для родителя и детского приложения статус доставки сообщений Telegram.
/// Внутренние причины, адреса и ошибки VPN намеренно не передаются клиентам.
/// </summary>
public sealed class TelegramBotAvailabilityResponse
{
    /// <summary>Доступен ли Telegram-бот для отправки новых сообщений.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>Когда бот в последний раз сообщил о своём состоянии (UTC).</summary>
    public DateTime? LastCheckedAt { get; init; }

    /// <summary>Короткое безопасное пояснение для интерфейса.</summary>
    public string Message { get; init; } = string.Empty;
}
