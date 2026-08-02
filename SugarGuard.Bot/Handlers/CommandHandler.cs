using Microsoft.Extensions.Logging;
using SugarGuard.Bot.Keyboards;
using SugarGuard.Bot.Services;
using SugarGuard.Shared.Constants;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SugarGuard.Bot.Handlers;

/// <summary>
/// Обработчик команд бота (/start, /help, /connect).
/// Формат кода привязки — единый source of truth <see cref="ConnectionCodeFormat"/>.
/// </summary>
public class CommandHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<CommandHandler> _logger;
    private readonly MainMenuKeyboard _mainMenuKeyboard;
    private readonly Services.ApiClient _apiClient;
    private readonly IBotUserContextService _botUserContextService;
    private readonly TelegramRateLimiter _rateLimiter;
    private readonly ConnectionCodeEntrySessionService _connectionCodeEntrySessions;

    public CommandHandler(
        ITelegramBotClient botClient,
        ILogger<CommandHandler> logger,
        MainMenuKeyboard mainMenuKeyboard,
        Services.ApiClient apiClient,
        IBotUserContextService botUserContextService,
        TelegramRateLimiter rateLimiter,
        ConnectionCodeEntrySessionService connectionCodeEntrySessions)
    {
        _botClient = botClient;
        _logger = logger;
        _mainMenuKeyboard = mainMenuKeyboard;
        _apiClient = apiClient;
        _botUserContextService = botUserContextService;
        _rateLimiter = rateLimiter;
        _connectionCodeEntrySessions = connectionCodeEntrySessions;
    }

    /// <summary>
    /// Обрабатывает команды бота
    /// </summary>
    public async Task HandleCommandAsync(long chatId, long userId, string command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Обработка команды {Command} от пользователя {UserId}", command, userId);

        if (!_rateLimiter.TryAcquire(userId))
        {
            _logger.LogWarning("Rate limit: пользователь {UserId} превысил лимит запросов", userId);
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "⏳ Слишком много запросов. Подождите минуту и попробуйте снова.",
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            var commandName = command
                .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0]
                .Split('@', 2)[0]
                .ToLowerInvariant();

            switch (commandName)
            {
                case "/start":
                    await HandleStartCommandAsync(chatId, userId, cancellationToken);
                    break;

                case "/help":
                    await SendHelpAsync(chatId, cancellationToken);
                    break;

                case "/connect":
                    await HandleConnectCommandAsync(chatId, userId, command, cancellationToken);
                    break;

                default:
                    await HandleUnknownCommandAsync(chatId, command, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке команды {Command}", command);
            await SendErrorMessageAsync(chatId, cancellationToken);
        }
    }

    /// <summary>
    /// Обрабатывает команду /start - приветствие и главное меню
    /// </summary>
    private async Task HandleStartCommandAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        _connectionCodeEntrySessions.Complete(userId);

        var welcomeMessage = $"""
            🍭 Добро пожаловать в SugarGuard Bot!
            
            Я помогу вам следить за состоянием вашего ребёнка с диабетом:
            
            📊 Получать уведомления об измерениях глюкозы
            🎒 Управлять рюкзаком с перекусами
            📈 Просматривать статистику и экспортировать отчёты
            ⚠️ Получать экстренные уведомления при критических уровнях
            
            Для начала работы нажмите кнопку «🔗 Подключить ребёнка» ниже.
            Бот попросит только код из веб-кабинета родителя — команду вводить не потребуется.
            
            Выберите действие из меню ниже:
            """;

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: welcomeMessage,
            replyMarkup: _mainMenuKeyboard.GetKeyboard(),
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );

        _logger.LogInformation("Отправлено приветственное сообщение пользователю {UserId}", userId);
    }

    /// <summary>
    /// Обрабатывает команду /help - справка по командам
    /// </summary>
    public async Task SendHelpAsync(long chatId, CancellationToken cancellationToken)
    {
        var helpMessage = $"""
            📖 **Справка по командам SugarGuard Bot**
            
            **Основные команды:**
            /start - Главное меню и приветствие
            /help - Эта справка
            Кнопка «🔗 Подключить ребёнка» - безопасная привязка уведомлений
            
            **Функции бота:**
            
            🎒 **Рюкзак**
            • Просмотр текущих перекусов ребёнка
            • Добавление новых перекусов
            • Удаление перекусов
            
            📊 **Статистика**
            • Просмотр измерений за день/неделю/месяц/год
            • Экспорт данных в PDF для врача
            • Анализ времени в целевом диапазоне
            
            ⚙️ **Настройки**
            • Выбор активного ребёнка, если их несколько
            
            **Уведомления:**
            • 📈 Новые измерения глюкозы
            • 🍪 Съеденные перекусы
            • ⚠️ Критические уровни с геолокацией
            • ⏰ Пропущенные измерения
            
            **Получение кода привязки:**
            1. Откройте веб-кабинет родителя SugarGuard
            2. Перейдите: «Настройки» → «Telegram-бот» → «Получить код»
            3. Нажмите в боте «🔗 Подключить ребёнка»
            4. Введите полученный код без команды
            
            При возникновении проблем обратитесь к разработчикам.
            """;

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: helpMessage,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );

        _logger.LogInformation("Отправлена справка в чат {ChatId}", chatId);
    }

    /// <summary>
    /// Обрабатывает команду /connect с кодом привязки.
    /// Формат проверяется через <see cref="ConnectionCodeFormat.IsValid"/> —
    /// единый source of truth с API DTO.
    /// </summary>
    private async Task HandleConnectCommandAsync(long chatId, long userId, string command, CancellationToken cancellationToken)
    {
        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            await BeginConnectionAsync(chatId, userId, cancellationToken);
            return;
        }

        await SubmitConnectionCodeAsync(chatId, userId, parts[1], cancellationToken);
    }

    /// <summary>
    /// Открывает сценарий подключения из инлайн-кнопки. Пользователь вводит код,
    /// не набирая техническую команду <c>/connect</c>.
    /// </summary>
    public async Task BeginConnectionAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        _connectionCodeEntrySessions.Begin(userId);

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: """
                🔗 **Подключение Telegram-бота**

                1. В веб-кабинете родителя откройте «Настройки» → «Telegram-бот».
                2. Нажмите «Получить код».
                3. Отправьте сюда полученный код — без команды и без лишнего текста.

                Код действует 10 минут и используется только для привязки этого чата к уведомлениям ребёнка.
                """,
            parseMode: ParseMode.Markdown,
            replyMarkup: new InlineKeyboardMarkup([
                [InlineKeyboardButton.WithCallbackData("Отменить", "cancel_connect")],
                [InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "main_menu")]
            ]),
            cancellationToken: cancellationToken);
    }

    /// <summary>Отменяет сценарий ввода кода подключения.</summary>
    public async Task CancelConnectionAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        ClearPendingConnection(userId);
        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Подключение отменено. Его можно начать снова кнопкой «🔗 Подключить ребёнка».",
            replyMarkup: _mainMenuKeyboard.GetKeyboard(),
            cancellationToken: cancellationToken);
    }

    /// <summary>Очищает незавершённый сценарий подключения без отправки сообщения.</summary>
    public void ClearPendingConnection(long userId) => _connectionCodeEntrySessions.Complete(userId);

    /// <summary>Проверяет код, введённый в сценарии привязки.</summary>
    public async Task SubmitConnectionCodeAsync(long chatId, long userId, string rawCode, CancellationToken cancellationToken)
    {
        rawCode = rawCode.Trim();

        if (!ConnectionCodeFormat.IsValid(rawCode, normalize: true))
        {
            await SendConnectFormatErrorAsync(chatId, userId, rawCode, cancellationToken);
            return;
        }

        var connectionCode = ConnectionCodeFormat.Normalize(rawCode)!;
        _logger.LogInformation("Попытка привязки Telegram-пользователя {UserId}", userId);

        // Отправляем сообщение о проверке кода
        var processingMessage = await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "🔄 Проверяю код привязки...",
            cancellationToken: cancellationToken
        );

        try
        {
            // Проверяем код привязки через API
            var childId = await VerifyConnectionCodeAsync(userId, connectionCode, cancellationToken);

            if (childId.HasValue)
            {
                // Сразу выбираем ребёнка для команд бота. Привязка в API уже создана
                // проверкой кода, но без активного контекста кнопки не знают, чьи данные показывать.
                var contextSaved = await _botUserContextService.SetCurrentChildIdAsync(
                    userId,
                    childId.Value,
                    cancellationToken);

                if (!contextSaved)
                {
                    // Не отменяем успешную привязку: GetCurrentChildIdAsync самостоятельно
                    // выберет единственного привязанного ребёнка при следующем действии.
                    _logger.LogWarning(
                        "Привязка Telegram-пользователя {UserId} успешна, но активный ChildId не сохранён",
                        userId);
                }

                _connectionCodeEntrySessions.Complete(userId);
                var successMessage = """
                    ✅ **Привязка успешна!**
                    
                    Теперь вы будете получать уведомления о:
                    • 📊 Измерениях глюкозы
                    • 🍪 Съеденных перекусах
                    • ⚠️ Критических ситуациях
                    • ⏰ Пропущенных измерениях
                    
                    Используйте меню ниже для управления данными ребёнка.
                    """;

                await _botClient.EditMessageTextAsync(
                    chatId: chatId,
                    messageId: processingMessage.MessageId,
                    text: successMessage,
                    replyMarkup: _mainMenuKeyboard.GetKeyboard(),
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation("Успешная привязка Telegram-пользователя {UserId}", userId);
            }
            else
            {
                var failureMessage = """
                    ❌ **Неверный или просроченный код**
                    
                    Возможные причины:
                    • Код введён неправильно
                    • Код уже использован
                    • Прошло более 10 минут с момента генерации
                    
                    Получите новый код в веб-кабинете родителя и повторите подключение кнопкой ниже.
                    """;

                await _botClient.EditMessageTextAsync(
                    chatId: chatId,
                    messageId: processingMessage.MessageId,
                    text: failureMessage,
                    replyMarkup: _mainMenuKeyboard.GetKeyboard(),
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );

                _logger.LogWarning("Неудачная привязка Telegram-пользователя {UserId}", userId);
                _connectionCodeEntrySessions.Complete(userId);
            }
        }
        catch (Exception ex)
        {
            _connectionCodeEntrySessions.Complete(userId);
            _logger.LogError(ex, "Ошибка при проверке кода привязки для пользователя {UserId}", userId);

            var errorMessage = """
                ❌ **Ошибка при проверке кода**
                
                Произошла техническая ошибка. Попробуйте позже или обратитесь к разработчикам.
                """;

            await _botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: processingMessage.MessageId,
                text: errorMessage,
                replyMarkup: _mainMenuKeyboard.GetKeyboard(),
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
        }
    }

    /// <summary>
    /// Обрабатывает неизвестные команды
    /// </summary>
    private async Task HandleUnknownCommandAsync(long chatId, string command, CancellationToken cancellationToken)
    {
        var message = $"""
            ❓ **Неизвестная команда:** `{command}`
            
            Доступные команды:
            /start - Главное меню
            /help - Справка
            «🔗 Подключить ребёнка» — привязка уведомлений
            
            Используйте /help для подробной справки.
            """;

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: message,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );

        _logger.LogInformation("Отправлено сообщение о неизвестной команде {Command} в чат {ChatId}", command, chatId);
    }

    /// <summary>
    /// Отправляет сообщение об ошибке
    /// </summary>
    private async Task SendErrorMessageAsync(long chatId, CancellationToken cancellationToken)
    {
        var errorMessage = """
            ❌ **Произошла ошибка**

            Попробуйте повторить операцию позже или обратитесь к разработчикам.
            """;

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: errorMessage,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Отправляет пользователю сообщение о неверном формате кода подключения
    /// и логирует предупреждение. Текст описывает формат,
    /// синхронизированный с <see cref="ConnectionCodeFormat"/>.
    /// </summary>
    private async Task SendConnectFormatErrorAsync(long chatId, long userId, string enteredValue, CancellationToken cancellationToken)
    {
        var errorMessage = $"""
            ❌ **Неверный формат кода**

            Введите только код: `{ConnectionCodeFormat.Format(ConnectionCodeFormat.Generate())}`

            Где:
            • {ConnectionCodeFormat.Length} символов из алфавита A–Z (без I, O) + 2–9 (без 0, 1)
            • Допускается дефис-разделитель посередине: ABCD-1234 (4 буквы + дефис + 4 цифры)
            • Регистр не важен (ввод в любом регистре)

            Пример: `ABCD-1234`

            Код выдаётся в веб-кабинете родителя:
            «Настройки» → «Telegram-бот» → «Получить код».
            Проверьте код и отправьте его ещё раз в этот чат.
            """;

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: errorMessage,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );

        _logger.LogWarning("Неверный формат кода подключения от пользователя {UserId}.", userId);
    }

    /// <summary>
    /// Проверяет код привязки через API
    /// </summary>
    private async Task<Guid?> VerifyConnectionCodeAsync(long userId, string connectionCode, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _apiClient.VerifyConnectionCodeAsync(
                connectionCode, 
                userId, 
                cancellationToken: cancellationToken);

            if (response.Success && response.IsValid && response.ChildId.HasValue)
            {
                _logger.LogInformation(
                    "Код подключения успешно проверен для пользователя {UserId}; ChildId={ChildId}",
                    userId,
                    response.ChildId);
                return response.ChildId.Value;
            }

            _logger.LogWarning("Код подключения недействителен для пользователя {UserId}: {Error}",
                userId, response.ErrorMessage);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке кода привязки для пользователя {UserId}", userId);
            return null;
        }
    }
}
