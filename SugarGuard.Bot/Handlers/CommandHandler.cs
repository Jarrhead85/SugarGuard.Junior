using Microsoft.Extensions.Logging;
using SugarGuard.Bot.Keyboards;
using SugarGuard.Bot.Services;
using SugarGuard.Shared.Constants;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
    private readonly TelegramRateLimiter _rateLimiter;

    public CommandHandler(
        ITelegramBotClient botClient,
        ILogger<CommandHandler> logger,
        MainMenuKeyboard mainMenuKeyboard,
        Services.ApiClient apiClient,
        TelegramRateLimiter rateLimiter)
    {
        _botClient = botClient;
        _logger = logger;
        _mainMenuKeyboard = mainMenuKeyboard;
        _apiClient = apiClient;
        _rateLimiter = rateLimiter;
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
            switch (command.Split(' ')[0].ToLower())
            {
                case "/start":
                    await HandleStartCommandAsync(chatId, userId, cancellationToken);
                    break;

                case "/help":
                    await HandleHelpCommandAsync(chatId, cancellationToken);
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
        var groupSize = InviteCodeLimits.GroupSize;
        var exampleCode = new string('A', groupSize) + new string('1', groupSize);
        var formattedExample = InviteCodeLimits.Format(exampleCode);

        var welcomeMessage = $"""
            🍭 Добро пожаловать в SugarGuard Bot!
            
            Я помогу вам следить за состоянием вашего ребёнка с диабетом:
            
            📊 Получать уведомления об измерениях глюкозы
            🎒 Управлять рюкзаком с перекусами
            📈 Просматривать статистику и экспортировать отчёты
            ⚠️ Получать экстренные уведомления при критических уровнях
            
            Для начала работы привяжите бота к приложению ребёнка командой:
            /connect {formattedExample}
            
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
    private async Task HandleHelpCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var groupSize = InviteCodeLimits.GroupSize;
        var exampleCode = new string('A', groupSize) + new string('1', groupSize);
        var formattedExample = InviteCodeLimits.Format(exampleCode);

        var helpMessage = $"""
            📖 **Справка по командам SugarGuard Bot**
            
            **Основные команды:**
            /start - Главное меню и приветствие
            /help - Эта справка
            /connect {formattedExample} - Привязка к ребёнку (код из приложения)
            
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
            • Управление уведомлениями
            • Настройка расписания измерений
            
            **Уведомления:**
            • 📈 Новые измерения глюкозы
            • 🍪 Съеденные перекусы
            • ⚠️ Критические уровни с геолокацией
            • ⏰ Пропущенные измерения
            
            **Получение кода привязки:**
            1. Откройте приложение SugarGuard на телефоне ребёнка
            2. Перейдите в настройки → "Привязать родителя"
            3. Нажмите "Сгенерировать код"
            4. Введите код в боте: /connect {formattedExample}
            
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
        // Извлекаем код из команды: ожидаем "/connect <код>" (с любыми пробелами).
        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            await SendConnectFormatErrorAsync(chatId, userId, command, cancellationToken);
            return;
        }

        var rawCode = parts[1];

        // Единый source of truth — ConnectionCodeFormat.
        // Нормализация (uppercase, strip дефиса) выполняется внутри IsValid.
        if (!ConnectionCodeFormat.IsValid(rawCode, normalize: true))
        {
            await SendConnectFormatErrorAsync(chatId, userId, command, cancellationToken);
            return;
        }

        var connectionCode = ConnectionCodeFormat.Normalize(rawCode)!;
        _logger.LogInformation("Попытка привязки пользователя {UserId} с кодом {Code}", userId, connectionCode);

        // Отправляем сообщение о проверке кода
        var processingMessage = await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "🔄 Проверяю код привязки...",
            cancellationToken: cancellationToken
        );

        try
        {
            // Проверяем код привязки через API
            var isValidCode = await VerifyConnectionCodeAsync(userId, connectionCode, cancellationToken);

            if (isValidCode)
            {
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

                _logger.LogInformation("Успешная привязка пользователя {UserId} с кодом {Code}", userId, connectionCode);
            }
            else
            {
                var failureMessage = """
                    ❌ **Неверный или просроченный код**
                    
                    Возможные причины:
                    • Код введён неправильно
                    • Код уже использован
                    • Прошло более 10 минут с момента генерации
                    
                    Сгенерируйте новый код в приложении и попробуйте снова.
                    """;

                await _botClient.EditMessageTextAsync(
                    chatId: chatId,
                    messageId: processingMessage.MessageId,
                    text: failureMessage,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );

                _logger.LogWarning("Неудачная привязка пользователя {UserId} с кодом {Code}", userId, connectionCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке кода привязки {Code} для пользователя {UserId}", connectionCode, userId);

            var errorMessage = """
                ❌ **Ошибка при проверке кода**
                
                Произошла техническая ошибка. Попробуйте позже или обратитесь к разработчикам.
                """;

            await _botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: processingMessage.MessageId,
                text: errorMessage,
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
        var groupSize = InviteCodeLimits.GroupSize;
        var exampleCode = new string('A', groupSize) + new string('1', groupSize);
        var formattedExample = InviteCodeLimits.Format(exampleCode);

        var message = $"""
            ❓ **Неизвестная команда:** `{command}`
            
            Доступные команды:
            /start - Главное меню
            /help - Справка
            /connect {formattedExample} - Привязка к ребёнку
            
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
    /// Отправляет пользователю сообщение о неверном формате кода /connect
    /// и логирует предупреждение. Текст описывает формат,
    /// синхронизированный с <see cref="ConnectionCodeFormat"/>.
    /// </summary>
    private async Task SendConnectFormatErrorAsync(long chatId, long userId, string command, CancellationToken cancellationToken)
    {
        var errorMessage = $"""
            ❌ **Неверный формат кода**

            Используйте: `/connect {ConnectionCodeFormat.Format(ConnectionCodeFormat.Generate())}`

            Где:
            • {ConnectionCodeFormat.Length} символов из алфавита A–Z (без I, O) + 2–9 (без 0, 1)
            • Допускается дефис-разделитель посередине: ABCD-1234 (4 буквы + дефис + 4 цифры)
            • Регистр не важен (ввод в любом регистре)

            Пример: `/connect ABCD-1234`

            Код можно получить в приложении ребёнка:
            Настройки → Привязать родителя → Сгенерировать код
            """;

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: errorMessage,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );

        _logger.LogWarning("Неверный формат команды connect от пользователя {UserId}: {Command}", userId, command);
    }

    /// <summary>
    /// Проверяет код привязки через API
    /// </summary>
    private async Task<bool> VerifyConnectionCodeAsync(long userId, string connectionCode, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _apiClient.VerifyConnectionCodeAsync(
                connectionCode, 
                userId, 
                cancellationToken: cancellationToken);

            if (response.Success && response.IsValid)
            {
                _logger.LogInformation("✓ Код {Code} успешно проверен для пользователя {UserId}", 
                    connectionCode, userId);
                return true;
            }
            else
            {
                _logger.LogWarning("Код {Code} недействителен для пользователя {UserId}: {Error}", 
                    connectionCode, userId, response.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке кода привязки {Code} для пользователя {UserId}", 
                connectionCode, userId);
            return false;
        }
    }
}