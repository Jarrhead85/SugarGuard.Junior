namespace SugarGuard.MaxBot.Abstractions;

/// <summary>Низкоуровневый клиент официального API бота мессенджера MAX.</summary>
public interface IMaxBotClient
{
    bool IsConfigured { get; }
    string? PublicBotUrl { get; }

    Task SendTextAsync(long maxUserId, string message, CancellationToken cancellationToken = default);
    Task RegisterWebhookAsync(CancellationToken cancellationToken = default);
}
