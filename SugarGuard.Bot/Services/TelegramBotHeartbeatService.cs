using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace SugarGuard.Bot.Services;

/// <summary>
/// Периодически проверяет доступность Telegram и передаёт статус в SugarGuard API.
/// </summary>
public sealed class TelegramBotHeartbeatService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private readonly ITelegramBotClient _bot;
    private readonly TelegramOutboxClient _outbox;
    private readonly ILogger<TelegramBotHeartbeatService> _logger;
    private readonly string _version;

    public TelegramBotHeartbeatService(
        ITelegramBotClient bot,
        TelegramOutboxClient outbox,
        ILogger<TelegramBotHeartbeatService> logger)
    {
        _bot = bot;
        _outbox = outbox;
        _logger = logger;
        _version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var telegramAvailable = false;
            string? error = null;

            try
            {
                await _bot.GetMeAsync(cancellationToken: stoppingToken);
                telegramAvailable = true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                _logger.LogWarning(exception, "Telegram недоступен во время heartbeat-проверки.");
            }

            try
            {
                await _outbox.ReportHeartbeatAsync(new BotHeartbeatRequest
                {
                    InternetAvailable = telegramAvailable,
                    ExternalApiAvailable = telegramAvailable,
                    Error = error,
                    Version = _version
                }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Не удалось передать heartbeat Telegram-бота в API.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
