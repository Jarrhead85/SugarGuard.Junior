using Microsoft.Extensions.Logging;
using SugarGuard.Bot.Keyboards;
using System.Collections.Concurrent;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace SugarGuard.Bot.Services;

/// <summary>
/// Сервис для управления рюкзаком через Telegram-бота
/// Обрабатывает диалоги добавления перекусов и отображение содержимого рюкзака
/// </summary>
public class BackpackBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ApiClient _apiClient;
    private readonly BackpackKeyboard _backpackKeyboard;
    private readonly ILogger<BackpackBotService> _logger;

    /// <summary>
    /// TTL для состояния диалога: если пользователь не активен в течение этого времени,
    /// его state автоматически удаляется (cleanup в <see cref="GetOrUpdateStateAsync"/>).
    /// </summary>
    private static readonly TimeSpan DialogTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Максимальное количество одновременных диалогов (защита от memory leak / DoS).
    /// При превышении — самый старый state удаляется (LRU-эвикция).
    /// </summary>
    private const int MaxConcurrentDialogs = 10_000;

    // Хранилище состояний пользователей для диалогов.
    private readonly ConcurrentDictionary<long, UserDialogState> _userStates = new();

    public BackpackBotService(
        ITelegramBotClient botClient,
        ApiClient apiClient,
        BackpackKeyboard backpackKeyboard,
        ILogger<BackpackBotService> logger)
    {
        _botClient = botClient;
        _apiClient = apiClient;
        _backpackKeyboard = backpackKeyboard;
        _logger = logger;
    }

    /// <summary>
    /// Возвращает state для userId, проверяя TTL. Если state истёк — удаляет и возвращает null.
    /// </summary>
    private UserDialogState? GetOrUpdateState(long userId)
    {
        if (!_userStates.TryGetValue(userId, out var state))
            return null;

        if (DateTime.UtcNow - state.StartedAt > DialogTtl)
        {
            _userStates.TryRemove(userId, out _);
            _logger.LogDebug("State для {UserId} истёк (TTL {Ttl} мин), удалён", userId, DialogTtl.TotalMinutes);
            return null;
        }
        return state;
    }

    /// <summary>
    /// Сохраняет state с проверкой лимита
    /// </summary>
    private void SaveState(long userId, UserDialogState state)
    {
        if (_userStates.Count >= MaxConcurrentDialogs)
        {
            var oldest = _userStates
                .OrderBy(kv => kv.Value.StartedAt)
                .FirstOrDefault();
            if (!oldest.Equals(default(KeyValuePair<long, UserDialogState>)))
            {
                _userStates.TryRemove(oldest.Key, out _);
                _logger.LogWarning(
                    "Превышен лимит {Max} одновременных диалогов. Удалён самый старый state для {UserId}.",
                    MaxConcurrentDialogs, oldest.Key);
            }
        }
        _userStates[userId] = state;
    }

    /// <summary>
    /// Отображает содержимое рюкзака ребёнка
    /// </summary>
    public async Task ShowBackpackAsync(long chatId, long userId, Guid childId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Отображение рюкзака для пользователя {UserId}, ребёнок {ChildId}", userId, childId);

            // Получаем содержимое рюкзака через API
            var backpack = await _apiClient.GetBackpackAsync(childId, cancellationToken);
            
            if (backpack == null)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "❌ Не удалось получить содержимое рюкзака. Попробуйте позже.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            // Формируем сообщение с содержимым рюкзака
            var message = FormatBackpackMessage(backpack);
            
            // Преобразуем данные для клавиатуры
            var snacks = backpack.Items.Select(item => new BackpackSnack
            {
                BackpackItemId = item.BackpackItemId,
                SnackName = item.SnackName,
                BreadUnits = item.BreadUnits,
                CreatedAt = item.CreatedAt
            }).ToList();

            // Создаём клавиатуру с перекусами
            var keyboard = _backpackKeyboard.GetBackpackKeyboard(snacks);

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Рюкзак отображён для пользователя {UserId}: {ItemCount} перекусов", 
                userId, backpack.TotalItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отображении рюкзака для пользователя {UserId}", userId);
            
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Произошла ошибка при получении рюкзака. Попробуйте позже.",
                cancellationToken: cancellationToken
            );
        }
    }

    /// <summary>
    /// Начинает диалог добавления нового перекуса
    /// </summary>
    public async Task StartAddSnackDialogAsync(long chatId, long userId, Guid childId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Начало диалога добавления перекуса для пользователя {UserId}", userId);

            SaveState(userId, new UserDialogState
            {
                State = DialogState.WaitingForSnackName,
                ChildId = childId,
                StartedAt = DateTime.UtcNow
            });

            var message = """
                ➕ **Добавление нового перекуса**
                
                Введите название перекуса:
                
                _Например: Яблоко, Печенье, Йогурт_
                """;

            var keyboard = _backpackKeyboard.GetCancelAddKeyboard();

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при начале диалога добавления перекуса для пользователя {UserId}", userId);
        }
    }

    /// <summary>
    /// Обрабатывает текстовое сообщение в контексте диалога добавления перекуса
    /// </summary>
    public async Task<bool> HandleAddSnackMessageAsync(long chatId, long userId, string messageText, CancellationToken cancellationToken)
    {
        var state = GetOrUpdateState(userId);
        if (state is null)
        {
            return false; // Пользователь не в диалоге или state истёк
        }

        try
        {
            switch (state.State)
            {
                case DialogState.WaitingForSnackName:
                    return await HandleSnackNameInputAsync(chatId, userId, messageText, state, cancellationToken);
                
                case DialogState.WaitingForBreadUnits:
                    return await HandleBreadUnitsInputAsync(chatId, userId, messageText, state, cancellationToken);
                
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке сообщения в диалоге для пользователя {UserId}", userId);
            
            // Очищаем состояние при ошибке
            _userStates.TryRemove(userId, out _);
            
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Произошла ошибка. Попробуйте добавить перекус заново.",
                cancellationToken: cancellationToken
            );
            
            return true;
        }
    }

    /// <summary>
    /// Отменяет диалог добавления перекуса
    /// </summary>
    public async Task CancelAddSnackDialogAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        _userStates.TryRemove(userId, out _);
        
        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "❌ Добавление перекуса отменено.",
            cancellationToken: cancellationToken
        );
        
        _logger.LogInformation("Диалог добавления перекуса отменён для пользователя {UserId}", userId);
    }

    /// <summary>
    /// Проверяет, находится ли пользователь в диалоге добавления перекуса
    /// </summary>
    public bool IsUserInAddSnackDialog(long userId)
    {
        return GetOrUpdateState(userId) is not null;
    }

    /// <summary>
    /// Показывает подтверждение удаления перекуса
    /// </summary>
    public async Task ShowDeleteConfirmationAsync(long chatId, long userId, Guid backpackItemId, string snackName, CancellationToken cancellationToken)
    {
        try
        {
            var safeName = MarkdownSafe.EscapeMarkdownV1(MarkdownSafe.Truncate(snackName));
            _logger.LogInformation("Запрос подтверждения удаления перекуса {SnackName} для пользователя {UserId}", safeName, userId);

            var message = $"""
                🗑️ **Удаление перекуса**

                Вы действительно хотите удалить перекус **{safeName}**?

                ⚠️ Это действие нельзя отменить.
                """;

            var keyboard = _backpackKeyboard.GetDeleteConfirmationKeyboard(snackName, backpackItemId);

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отображении подтверждения удаления для пользователя {UserId}", userId);
        }
    }

    /// <summary>
    /// Удаляет перекус из рюкзака
    /// </summary>
    public async Task DeleteSnackAsync(long chatId, long userId, Guid backpackItemId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Удаление перекуса {ItemId} пользователем {UserId}", backpackItemId, userId);

            var success = await _apiClient.RemoveSnackAsync(backpackItemId, cancellationToken);

            if (success)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "✅ Перекус успешно удалён из рюкзака.",
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation("Перекус {ItemId} успешно удалён пользователем {UserId}", backpackItemId, userId);
            }
            else
            {
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "❌ Не удалось удалить перекус. Попробуйте позже.",
                    cancellationToken: cancellationToken
                );

                _logger.LogWarning("Не удалось удалить перекус {ItemId} для пользователя {UserId}", backpackItemId, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении перекуса {ItemId} для пользователя {UserId}", backpackItemId, userId);
            
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Произошла ошибка при удалении перекуса.",
                cancellationToken: cancellationToken
            );
        }
    }

    /// <summary>
    /// Показывает информацию о перекусе (неактивная кнопка)
    /// </summary>
    public Task ShowSnackInfoAsync(long chatId, long userId, Guid backpackItemId, CancellationToken cancellationToken)
    {
        // Эта функция вызывается при нажатии на название перекуса
        // Можно показать дополнительную информацию или просто проигнорировать
        _logger.LogInformation("Запрос информации о перекусе {ItemId} от пользователя {UserId}", backpackItemId, userId);
        
        // Пока что ничего не делаем, так как это неактивная кнопка
        return Task.CompletedTask;
    }

    private async Task<bool> HandleSnackNameInputAsync(long chatId, long userId, string snackName, UserDialogState state, CancellationToken cancellationToken)
    {
        // Валидация названия перекуса
        if (string.IsNullOrWhiteSpace(snackName) || snackName.Length > 500)
        {
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Название перекуса должно содержать от 1 до 500 символов. Попробуйте ещё раз:",
                cancellationToken: cancellationToken
            );
            return true;
        }

        // Сохраняем название и переходим к следующему шагу
        state.SnackName = snackName.Trim();
        state.State = DialogState.WaitingForBreadUnits;

        var safeName = MarkdownSafe.EscapeMarkdownV1(snackName.Trim());

        var message = $"""
            ✅ Название: **{safeName}**

            Теперь введите количество хлебных единиц (ХЕ):

            _Например: 1.5, 2, 0.5_
            """;

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: message,
            parseMode: ParseMode.Markdown,
            replyMarkup: _backpackKeyboard.GetCancelAddKeyboard(),
            cancellationToken: cancellationToken
        );

        return true;
    }

    private async Task<bool> HandleBreadUnitsInputAsync(long chatId, long userId, string breadUnitsText, UserDialogState state, CancellationToken cancellationToken)
    {
        // Валидация хлебных единиц
        if (!decimal.TryParse(breadUnitsText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var breadUnits) ||
            breadUnits < 0.1m || breadUnits > 99.99m)
        {
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Количество ХЕ должно быть числом от 0.1 до 99.99. Попробуйте ещё раз:",
                cancellationToken: cancellationToken
            );
            return true;
        }

        // Добавляем перекус через API
        var result = await _apiClient.AddSnackAsync(state.ChildId, state.SnackName!, breadUnits, cancellationToken);
        
        // Очищаем состояние диалога
        _userStates.TryRemove(userId, out _);

        if (result != null)
        {
            var successMessage = $"""
                ✅ **Перекус добавлен!**
                
                📝 Название: {result.SnackName}
                🍞 Хлебные единицы: {result.BreadUnits} ХЕ
                ⏰ Добавлено: {result.CreatedAt:HH:mm dd.MM.yyyy}
                """;

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: successMessage,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Перекус {SnackName} ({BreadUnits} ХЕ) добавлен пользователем {UserId}", 
                result.SnackName, result.BreadUnits, userId);
        }
        else
        {
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Не удалось добавить перекус. Попробуйте позже.",
                cancellationToken: cancellationToken
            );
        }

        return true;
    }

    private static string FormatBackpackMessage(BackpackResponse backpack)
    {
        var message = new System.Text.StringBuilder();
        message.AppendLine("🎒 **Рюкзак ребёнка**");
        message.AppendLine();

        if (backpack.TotalItems == 0)
        {
            message.AppendLine("📭 Рюкзак пуст");
            message.AppendLine();
            message.AppendLine("Добавьте перекусы, чтобы ребёнок мог выбирать их при получении рекомендаций.");
        }
        else
        {
            message.AppendLine($"📊 **Всего перекусов:** {backpack.TotalItems}");
            message.AppendLine($"🍞 **Общее количество ХЕ:** {backpack.TotalBreadUnits:F1}");
            message.AppendLine($"🕐 **Последнее обновление:** {backpack.LastUpdated:HH:mm dd.MM.yyyy}");
            message.AppendLine();
            message.AppendLine("**Список перекусов:**");

            foreach (var item in backpack.Items.OrderBy(i => i.CreatedAt))
            {
                var safeName = MarkdownSafe.EscapeMarkdownV1(MarkdownSafe.Truncate(item.SnackName));
                message.AppendLine($"• {safeName} — {item.BreadUnits:F1} ХЕ");
            }
        }

        return message.ToString();
    }
}

/// <summary>
/// Состояние диалога пользователя
/// </summary>
public class UserDialogState
{
    public DialogState State { get; set; }
    public Guid ChildId { get; set; }
    public string? SnackName { get; set; }
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Состояния диалога добавления перекуса
/// </summary>
public enum DialogState
{
    WaitingForSnackName,
    WaitingForBreadUnits
}
