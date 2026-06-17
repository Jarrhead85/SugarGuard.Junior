using Microsoft.Extensions.Logging;
using SugarGuard.Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace SugarGuard.Bot.Handlers;

/// <summary>
/// Обработчик текстовых сообщений (не команд)
/// </summary>
public class MessageHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly BackpackBotService _backpackBotService;
    private readonly ILogger<MessageHandler> _logger;
    private readonly TelegramRateLimiter _rateLimiter;

    public MessageHandler(
        ITelegramBotClient botClient,
        BackpackBotService backpackBotService,
        ILogger<MessageHandler> logger,
        TelegramRateLimiter rateLimiter)
    {
        _botClient = botClient;
        _backpackBotService = backpackBotService;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Обрабатывает текстовые сообщения от пользователей
    /// </summary>
    public async Task HandleTextMessageAsync(long chatId, long userId, string messageText, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Обработка текстового сообщения от пользователя {UserId}: {MessageText}", userId, messageText);

        if (!_rateLimiter.TryAcquire(userId))
        {
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "⏳ Слишком много сообщений. Подождите минуту.",
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            // Проверяем, находится ли пользователь в диалоге добавления перекуса
            if (_backpackBotService.IsUserInAddSnackDialog(userId))
            {
                var handled = await _backpackBotService.HandleAddSnackMessageAsync(chatId, userId, messageText, cancellationToken);
                if (handled)
                {
                    return; // Сообщение обработано в контексте диалога
                }
            }

            // Обычная обработка текстовых сообщений
            var responseMessage = """
                💬 **Сообщение получено**
                
                Я понимаю только команды. Используйте:
                • /start - Главное меню
                • /help - Справка по командам
                • /connect XXXX-YY - Привязка к ребёнку
                
                Или выберите действие из меню кнопок.
                """;

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: responseMessage,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Отправлен ответ на текстовое сообщение пользователю {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке текстового сообщения от пользователя {UserId}", userId);
            
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Произошла ошибка при обработке сообщения.",
                cancellationToken: cancellationToken
            );
        }
    }
}