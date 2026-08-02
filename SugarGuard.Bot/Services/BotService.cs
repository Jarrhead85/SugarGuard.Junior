using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SugarGuard.Bot.Handlers;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SugarGuard.Bot.Services;

/// <summary>
/// Основной сервис Telegram-бота, отвечающий за polling и обработку обновлений
/// </summary>
public class BotService : BackgroundService
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromMinutes(2);
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<BotService> _logger;
    private readonly CommandHandler _commandHandler;
    private readonly CallbackHandler _callbackHandler;
    private readonly MessageHandler _messageHandler;

    public BotService(
        ITelegramBotClient botClient,
        ILogger<BotService> logger,
        CommandHandler commandHandler,
        CallbackHandler callbackHandler,
        MessageHandler messageHandler)
    {
        _botClient = botClient;
        _logger = logger;
        _commandHandler = commandHandler;
        _callbackHandler = callbackHandler;
        _messageHandler = messageHandler;
    }

    /// <summary>
    /// Запускает polling для получения обновлений от Telegram
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Запуск SugarGuard Bot...");

        // Настройки для получения обновлений
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message,
                UpdateType.CallbackQuery
            }
        };

        var retryDelay = InitialRetryDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Telegram может быть временно недоступен из-за VPN или сети.
                // Это не должно останавливать процесс и запускать бесконечный цикл systemd.
                var me = await _botClient.GetMeAsync(stoppingToken);
                _logger.LogInformation("Бот запущен: @{BotUsername} ({BotName})", me.Username, me.FirstName);

                _botClient.StartReceiving(
                    HandleUpdateAsync,
                    HandlePollingErrorAsync,
                    receiverOptions,
                    stoppingToken);

                _logger.LogInformation("Бот готов к работе.");
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Остановка бота...");
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Telegram временно недоступен ({ErrorType}). Повторная проверка через {RetryDelay}.",
                    exception.GetType().Name,
                    retryDelay);

                try
                {
                    await Task.Delay(retryDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Остановка бота...");
                    return;
                }

                retryDelay = TimeSpan.FromSeconds(Math.Min(
                    MaximumRetryDelay.TotalSeconds,
                    retryDelay.TotalSeconds * 2));
            }
        }
    }

    /// <summary>
    /// Обрабатывает входящие обновления от Telegram
    /// </summary>
    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await HandleMessageAsync(update.Message!, cancellationToken);
                    break;

                case UpdateType.CallbackQuery:
                    await HandleCallbackQueryAsync(update.CallbackQuery!, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Получен неподдерживаемый тип обновления: {UpdateType}", update.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Ошибка при обработке обновления {UpdateId} ({ErrorType})",
                update.Id,
                ex.GetType().Name);
        }
    }

    /// <summary>
    /// Обрабатывает текстовые сообщения
    /// </summary>
    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type != ChatType.Private)
        {
            _logger.LogWarning("Отклонено сообщение из не-личного чата {ChatId}", message.Chat.Id);
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Для защиты данных ребёнка SugarGuard Bot работает только в личном чате с ботом.",
                cancellationToken: cancellationToken);
            return;
        }

        if (message.Text is null)
        {
            _logger.LogDebug("Получено сообщение без текста от пользователя {UserId}", message.From?.Id);
            return;
        }

        var userId = message.From?.Id ?? 0;
        var chatId = message.Chat.Id;
        var messageText = message.Text;

        _logger.LogInformation(
            "Получено {MessageKind} от пользователя {UserId}.",
            messageText.StartsWith('/') ? "командное сообщение" : "текстовое сообщение",
            userId);

        // Проверяем, является ли сообщение командой
        if (messageText.StartsWith('/'))
        {
            await _commandHandler.HandleCommandAsync(chatId, userId, messageText, cancellationToken);
        }
        else
        {
            await _messageHandler.HandleTextMessageAsync(chatId, userId, messageText, cancellationToken);
        }
    }

    /// <summary>
    /// Обрабатывает callback-запросы от инлайн-кнопок
    /// </summary>
    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message?.Chat.Type is not ChatType.Private)
        {
            _logger.LogWarning("Отклонено нажатие кнопки из не-личного чата пользователя {UserId}", callbackQuery.From.Id);
            await _botClient.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: "Для защиты данных откройте личный чат с ботом.",
                showAlert: true,
                cancellationToken: cancellationToken);
            return;
        }

        var userId = callbackQuery.From.Id;
        var chatId = callbackQuery.Message?.Chat.Id ?? 0;
        var callbackData = callbackQuery.Data ?? string.Empty;

        _logger.LogInformation("Получен callback от пользователя {UserId}.", userId);

        await _callbackHandler.HandleCallbackAsync(chatId, userId, callbackData, callbackQuery.Id, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает ошибки polling
    /// </summary>
    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorCode = exception is ApiRequestException apiRequestException
            ? apiRequestException.ErrorCode
            : (int?)null;

        _logger.LogError(
            "Ошибка polling Telegram ({ErrorType}, код {ErrorCode}).",
            exception.GetType().Name,
            errorCode);
        return Task.CompletedTask;
    }
}
