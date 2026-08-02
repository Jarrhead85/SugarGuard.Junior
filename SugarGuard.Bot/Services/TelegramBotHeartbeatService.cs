using System.Reflection;
using System.Net.Sockets;
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
    private readonly string? _loopbackProxy;

    public TelegramBotHeartbeatService(
        ITelegramBotClient bot,
        TelegramOutboxClient outbox,
        ILogger<TelegramBotHeartbeatService> logger)
    {
        _bot = bot;
        _outbox = outbox;
        _logger = logger;
        _version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        _loopbackProxy = GetConfiguredLoopbackProxy();
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
                error = DescribeTelegramFailure(exception, _loopbackProxy);
                // Не передаём необработанное исключение в журнал: некоторые HTTP-клиенты
                // включают полный URL запроса, а токен Telegram является частью пути.
                _logger.LogWarning("Telegram недоступен во время heartbeat-проверки: {Reason}", error);
            }

            var controlPlaneAvailable = false;
            try
            {
                controlPlaneAvailable = await _outbox.IsControlPlaneAvailableAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Не удалось проверить доступность управляющего API Telegram-бота. Тип ошибки: {ErrorType}",
                    exception.GetBaseException().GetType().Name);
            }

            try
            {
                var reported = await _outbox.ReportHeartbeatAsync(new BotHeartbeatRequest
                {
                    // Доступность SugarGuard API и Telegram — независимые
                    // сигналы. Так интерфейсы отличают сбой VPN от падения
                    // самого домашнего сервера.
                    InternetAvailable = controlPlaneAvailable,
                    ExternalApiAvailable = telegramAvailable,
                    Error = error,
                    Version = _version
                }, stoppingToken);

                if (!reported)
                {
                    _logger.LogWarning("Управляющий API не принял heartbeat Telegram-бота.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Не удалось передать heartbeat Telegram-бота в API. Тип ошибки: {ErrorType}",
                    exception.GetBaseException().GetType().Name);
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

    private static string? GetConfiguredLoopbackProxy()
    {
        foreach (var variableName in new[] { "HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY" })
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsLoopback)
            {
                // Для статуса достаточно самого факта локального proxy. Не сохраняем
                // URL: в нём теоретически могли бы оказаться учётные данные.
                return "configured";
            }
        }

        return null;
    }

    private static string DescribeTelegramFailure(Exception exception, string? loopbackProxy)
    {
        if (!string.IsNullOrWhiteSpace(loopbackProxy) &&
            exception is HttpRequestException { InnerException: SocketException })
        {
            return "Happ VPN недоступен: локальный proxy не отвечает. Выполняется автоматическое восстановление.";
        }

        // Текст исключения потенциально содержит URL, параметры proxy или детали
        // транспорта. В heartbeat и централизованные журналы передаём только тип.
        var errorType = exception.GetBaseException().GetType().Name;
        return $"Telegram временно недоступен ({errorType}). Выполняется автоматическое восстановление.";
    }
}
