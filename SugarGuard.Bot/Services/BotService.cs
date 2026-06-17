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

        try
        {
            // Получаем информацию о боте
            var me = await _botClient.GetMeAsync(stoppingToken);
            _logger.LogInformation("Бот запущен: @{BotUsername} ({BotName})", me.Username, me.FirstName);

            // Запускаем polling
            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandlePollingErrorAsync,
                receiverOptions,
                stoppingToken
            );

            _logger.LogInformation("Бот готов к работе. Нажмите Ctrl+C для остановки.");

            // Ждём сигнала остановки
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Остановка бота...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка в работе бота");
            throw;
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
            _logger.LogError(ex, "Ошибка при обработке обновления {UpdateId}", update.Id);
        }
    }

    /// <summary>
    /// Обрабатывает текстовые сообщения
    /// </summary>
    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null)
        {
            _logger.LogDebug("Получено сообщение без текста от пользователя {UserId}", message.From?.Id);
            return;
        }

        var userId = message.From?.Id ?? 0;
        var chatId = message.Chat.Id;
        var messageText = message.Text;

        _logger.LogInformation("Получено сообщение от {UserId}: {MessageText}", userId, messageText);

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
        var userId = callbackQuery.From.Id;
        var chatId = callbackQuery.Message?.Chat.Id ?? 0;
        var callbackData = callbackQuery.Data ?? string.Empty;

        _logger.LogInformation("Получен callback от {UserId}: {CallbackData}", userId, callbackData);

        await _callbackHandler.HandleCallbackAsync(chatId, userId, callbackData, callbackQuery.Id, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает ошибки polling
    /// </summary>
    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Ошибка Telegram Bot API:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(exception, "Ошибка polling: {ErrorMessage}", errorMessage);
        return Task.CompletedTask;
    }
}