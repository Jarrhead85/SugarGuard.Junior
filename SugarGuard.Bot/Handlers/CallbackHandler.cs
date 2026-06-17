using Microsoft.Extensions.Logging;
using SugarGuard.Bot.Keyboards;
using SugarGuard.Bot.Services;
using Telegram.Bot;

namespace SugarGuard.Bot.Handlers;

/// <summary>
/// Обработчик callback-запросов от инлайн-кнопок
/// </summary>
public class CallbackHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly BackpackBotService _backpackBotService;
    private readonly StatisticsBotService _statisticsBotService;
    private readonly IBotUserContextService _contextService;
    private readonly MainMenuKeyboard _mainMenuKeyboard;
    private readonly ILogger<CallbackHandler> _logger;
    private readonly TelegramRateLimiter _rateLimiter;

    public CallbackHandler(
        ITelegramBotClient botClient,
        BackpackBotService backpackBotService,
        StatisticsBotService statisticsBotService,
        IBotUserContextService contextService,
        MainMenuKeyboard mainMenuKeyboard,
        ILogger<CallbackHandler> logger,
        TelegramRateLimiter rateLimiter)
    {
        _botClient = botClient;
        _backpackBotService = backpackBotService;
        _statisticsBotService = statisticsBotService;
        _contextService = contextService;
        _mainMenuKeyboard = mainMenuKeyboard;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Обрабатывает callback-запросы от инлайн-кнопок
    /// </summary>
    public async Task HandleCallbackAsync(long chatId, long userId, string callbackData, string callbackQueryId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Обработка callback {CallbackData} от пользователя {UserId}", callbackData, userId);

        if (!_rateLimiter.TryAcquire(userId))
        {
            // Для callback — showAlert во всплывающем окне Telegram.
            await _botClient.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQueryId,
                text: "⏳ Слишком много нажатий. Подождите минуту.",
                showAlert: true,
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            // Подтверждаем получение callback-запроса
            await _botClient.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQueryId,
                cancellationToken: cancellationToken
            );

            // Обрабатываем различные типы callback-запросов
            if (callbackData.StartsWith("delete_snack_") || callbackData.StartsWith("confirm_delete_") || 
                callbackData.StartsWith("snack_info_"))
            {
                await HandleSnackCallbackAsync(chatId, userId, callbackData, cancellationToken);
            }
            else if (callbackData.StartsWith("stats_") || callbackData.StartsWith("refresh_stats_") || 
                     callbackData.StartsWith("export_pdf_") || callbackData.StartsWith("nav_"))
            {
                await HandleStatisticsCallbackAsync(chatId, userId, callbackData, cancellationToken);
            }
            else
            {
                switch (callbackData)
                {
                    case "backpack":
                    case "show_backpack":
                        await HandleBackpackCallbackAsync(chatId, userId, cancellationToken);
                        break;

                    case "add_snack":
                        await HandleAddSnackCallbackAsync(chatId, userId, cancellationToken);
                        break;

                    case "cancel_add_snack":
                        await HandleCancelAddSnackCallbackAsync(chatId, userId, cancellationToken);
                        break;

                    case "cancel_delete":
                        await HandleCancelDeleteCallbackAsync(chatId, userId, cancellationToken);
                        break;

                    case "main_menu":
                        await HandleMainMenuCallbackAsync(chatId, userId, cancellationToken);
                        break;

                    case "empty_backpack":
                        // Неактивная кнопка, просто подтверждаем
                        break;

                    case "statistics":
                        await HandleStatisticsMenuCallbackAsync(chatId, userId, cancellationToken);
                        break;

                    case "settings":
                        await HandleSettingsCallbackAsync(chatId, userId, cancellationToken);
                        break;

                    default:
                        _logger.LogWarning("Неизвестный callback: {CallbackData}", callbackData);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке callback {CallbackData}", callbackData);
            
            // Уведомляем пользователя об ошибке через callback
            await _botClient.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQueryId,
                text: "❌ Произошла ошибка. Попробуйте позже.",
                showAlert: true,
                cancellationToken: cancellationToken
            );
        }
    }

    /// <summary>
    /// Обрабатывает нажатие на кнопку "Рюкзак"
    /// </summary>
    private async Task HandleBackpackCallbackAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        // Получаем текущий ChildId из контекста
        var childId = await _contextService.GetCurrentChildIdAsync(userId, cancellationToken);
        
        if (!childId.HasValue)
        {
            // Нет привязанного ребёнка
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Нет привязанного ребёнка. Используйте /connect XXXX-YY для привязки.",
                cancellationToken: cancellationToken
            );
            _logger.LogWarning("Попытка открыть рюкзак без привязанного ребёнка для пользователя {UserId}", userId);
            return;
        }
        
        await _backpackBotService.ShowBackpackAsync(chatId, userId, childId.Value, cancellationToken);
        _logger.LogInformation("Отображён рюкзак для пользователя {UserId}, ChildId={ChildId}", userId, childId);
    }

    /// <summary>
    /// Обрабатывает нажатие на кнопку "Добавить перекус"
    /// </summary>
    private async Task HandleAddSnackCallbackAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        // Получаем текущий ChildId из контекста
        var childId = await _contextService.GetCurrentChildIdAsync(userId, cancellationToken);
        
        if (!childId.HasValue)
        {
            // Нет привязанного ребёнка
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Нет привязанного ребёнка. Используйте /connect XXXX-YY для привязки.",
                cancellationToken: cancellationToken
            );
            _logger.LogWarning("Попытка добавить перекус без привязанного ребёнка для пользователя {UserId}", userId);
            return;
        }
        
        await _backpackBotService.StartAddSnackDialogAsync(chatId, userId, childId.Value, cancellationToken);
        _logger.LogInformation("Начат диалог добавления перекуса для пользователя {UserId}, ChildId={ChildId}", userId, childId);
    }

    /// <summary>
    /// Обрабатывает отмену добавления перекуса
    /// </summary>
    private async Task HandleCancelAddSnackCallbackAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        await _backpackBotService.CancelAddSnackDialogAsync(chatId, userId, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает отмену удаления перекуса
    /// </summary>
    private async Task HandleCancelDeleteCallbackAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "❌ Удаление отменено.",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Обрабатывает возврат в главное меню
    /// </summary>
    private async Task HandleMainMenuCallbackAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        var message = """
            🏠 **Главное меню**
            
            Выберите действие:
            """;

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: message,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: _mainMenuKeyboard.GetKeyboard(),
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Обрабатывает callback-запросы связанные с перекусами (удаление, информация)
    /// </summary>
    private async Task HandleSnackCallbackAsync(long chatId, long userId, string callbackData, CancellationToken cancellationToken)
    {
        try
        {
            if (callbackData.StartsWith("delete_snack_"))
            {
                // Извлекаем ID перекуса из callback данных
                var itemIdString = callbackData.Substring("delete_snack_".Length);
                if (Guid.TryParse(itemIdString, out var backpackItemId))
                {
                    // Получаем информацию о перекусе для подтверждения
                    // Название будет получено из API внутри ShowDeleteConfirmationAsync
                    var snackName = "перекус";
                    
                    await _backpackBotService.ShowDeleteConfirmationAsync(chatId, userId, backpackItemId, snackName, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Некорректный ID перекуса в callback: {CallbackData}", callbackData);
                }
            }
            else if (callbackData.StartsWith("confirm_delete_"))
            {
                // Подтверждение удаления
                var itemIdString = callbackData.Substring("confirm_delete_".Length);
                if (Guid.TryParse(itemIdString, out var backpackItemId))
                {
                    await _backpackBotService.DeleteSnackAsync(chatId, userId, backpackItemId, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Некорректный ID перекуса в callback подтверждения: {CallbackData}", callbackData);
                }
            }
            else if (callbackData.StartsWith("snack_info_"))
            {
                // Информация о перекусе (неактивная кнопка)
                var itemIdString = callbackData.Substring("snack_info_".Length);
                if (Guid.TryParse(itemIdString, out var backpackItemId))
                {
                    await _backpackBotService.ShowSnackInfoAsync(chatId, userId, backpackItemId, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке callback перекуса: {CallbackData}", callbackData);
            
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Произошла ошибка при обработке запроса.",
                cancellationToken: cancellationToken
            );
        }
    }

    /// <summary>
    /// Обрабатывает нажатие на кнопку "Статистика" (показывает меню выбора периода)
    /// </summary>
    private async Task HandleStatisticsMenuCallbackAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        await _statisticsBotService.ShowStatisticsMenuAsync(chatId, userId, cancellationToken);
        _logger.LogInformation("Отображено меню статистики для пользователя {UserId}", userId);
    }

    /// <summary>
    /// Обрабатывает callback-запросы связанные со статистикой
    /// </summary>
    private async Task HandleStatisticsCallbackAsync(long chatId, long userId, string callbackData, CancellationToken cancellationToken)
    {
        try
        {
            // Получаем текущий ChildId из контекста
            var childId = await _contextService.GetCurrentChildIdAsync(userId, cancellationToken);
            
            if (!childId.HasValue)
            {
                // Нет привязанного ребёнка
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "❌ Нет привязанного ребёнка. Используйте /connect XXXX-YY для привязки.",
                    cancellationToken: cancellationToken
                );
                _logger.LogWarning("Попытка получить статистику без привязанного ребёнка для пользователя {UserId}", userId);
                return;
            }

            if (callbackData.StartsWith("stats_"))
            {
                // Показать статистику за период (stats_day, stats_week, stats_month, stats_year)
                var period = callbackData.Substring("stats_".Length);
                await _statisticsBotService.ShowPeriodStatisticsAsync(chatId, userId, childId.Value, period, null, cancellationToken);
                _logger.LogInformation("Отображена статистика за {Period} для пользователя {UserId}, ChildId={ChildId}", period, userId, childId);
            }
            else if (callbackData.StartsWith("refresh_stats_"))
            {
                // Обновить статистику (refresh_stats_day, refresh_stats_week, etc.)
                var period = callbackData.Substring("refresh_stats_".Length);
                await _statisticsBotService.RefreshStatisticsAsync(chatId, userId, childId.Value, period, cancellationToken);
                _logger.LogInformation("Обновлена статистика за {Period} для пользователя {UserId}, ChildId={ChildId}", period, userId, childId);
            }
            else if (callbackData.StartsWith("export_pdf_"))
            {
                // Экспорт в PDF (export_pdf_day, export_pdf_week, etc.)
                var period = callbackData.Substring("export_pdf_".Length);
                await _statisticsBotService.ExportToPdfAsync(chatId, userId, childId.Value, period, cancellationToken);
                _logger.LogInformation("Запрос экспорта PDF за {Period} от пользователя {UserId}, ChildId={ChildId}", period, userId, childId);
            }
            else if (callbackData.StartsWith("nav_"))
            {
                // Навигация по периодам (nav_prev_day, nav_next_week, etc.)
                // Навигация будет реализована в следующей версии
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "🚧 Навигация по периодам будет реализована в следующих обновлениях.",
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке callback статистики: {CallbackData}", callbackData);
            
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Произошла ошибка при загрузке статистики.",
                cancellationToken: cancellationToken
            );
        }
    }

    /// <summary>
    /// Обрабатывает нажатие на кнопку "Настройки"
    /// </summary>
    private async Task HandleSettingsCallbackAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        var message = """
            ⚙️ **Настройки**
            
            Здесь будут настройки уведомлений и расписания.
            Функционал будет реализован в следующих задачах.
            """;

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: message,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            cancellationToken: cancellationToken
        );

        _logger.LogInformation("Отправлено сообщение о настройках пользователю {UserId}", userId);
    }
}