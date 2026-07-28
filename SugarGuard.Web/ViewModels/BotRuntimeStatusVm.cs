namespace SugarGuard.Web.ViewModels;

/// <summary>Состояние внешнего бота на странице администратора.</summary>
public sealed record BotRuntimeStatusVm(
    string BotName,
    bool IsConfigured,
    bool IsOnline,
    bool InternetAvailable,
    string? StatusMessage,
    DateTime? LastHeartbeatAt,
    DateTime? LastExternalApiSuccessAt,
    string? LastError,
    string? Version,
    int PendingTelegramMessages,
    int FailedTelegramMessages);
