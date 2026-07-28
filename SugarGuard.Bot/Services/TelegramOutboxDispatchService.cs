using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SugarGuard.Bot.Services;

/// <summary>
/// Доставляет уведомления из API через домашний бот с доступом к Telegram.
/// </summary>
public sealed class TelegramOutboxDispatchService : BackgroundService
{
    private readonly TelegramOutboxClient _outbox;
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<TelegramOutboxDispatchService> _logger;

    public TelegramOutboxDispatchService(TelegramOutboxClient outbox, ITelegramBotClient bot, ILogger<TelegramOutboxDispatchService> logger)
    {
        _outbox = outbox;
        _bot = bot;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var message in await _outbox.ClaimAsync(stoppingToken))
                {
                    await DeliverAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ошибка обработки очереди Telegram-уведомлений.");
            }
        }
    }

    private async Task DeliverAsync(TelegramOutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            InlineKeyboardMarkup? keyboard = message.RequiresAcknowledgement
                ? new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("✅ Я получил(а) тревогу", $"critical_alert_ack:{message.MessageId:N}"))
                : null;

            if (!message.TextDelivered)
            {
                await _bot.SendTextMessageAsync(
                    chatId: message.TelegramUserId,
                    text: message.Text,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
                await _outbox.MarkPartDeliveredAsync(message.MessageId, "text", cancellationToken);
            }

            if (message.Latitude.HasValue && message.Longitude.HasValue && !message.LocationDelivered)
            {
                await _bot.SendLocationAsync(
                    chatId: message.TelegramUserId,
                    latitude: message.Latitude.Value,
                    longitude: message.Longitude.Value,
                    cancellationToken: cancellationToken);
                await _outbox.MarkPartDeliveredAsync(message.MessageId, "location", cancellationToken);
            }

            await _outbox.CompleteAsync(message.MessageId, true, null, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось отправить Telegram-сообщение {MessageId}.", message.MessageId);
            await _outbox.CompleteAsync(message.MessageId, false, exception.Message, cancellationToken);
        }
    }
}
