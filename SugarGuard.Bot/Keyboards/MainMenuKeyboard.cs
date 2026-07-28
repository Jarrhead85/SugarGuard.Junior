using Telegram.Bot.Types.ReplyMarkups;

namespace SugarGuard.Bot.Keyboards;

/// <summary>
/// Главное меню бота с инлайн-кнопками
/// </summary>
public class MainMenuKeyboard
{
    /// <summary>
    /// Возвращает клавиатуру главного меню
    /// </summary>
    public InlineKeyboardMarkup GetKeyboard()
    {
        var keyboard = new InlineKeyboardButton[][]
        {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🎒 Рюкзак", "backpack"),
                InlineKeyboardButton.WithCallbackData("📊 Статистика", "statistics")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🩸 Последнее измерение", "last_measurement"),
                InlineKeyboardButton.WithCallbackData("🆘 Поддержка", "support")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("⚙️ Настройки", "settings"),
                InlineKeyboardButton.WithCallbackData("🔗 Подключить ребёнка", "connect")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("❓ Помощь", "help")
            }
        };

        return new InlineKeyboardMarkup(keyboard);
    }

    /// <summary>
    /// Возвращает компактную клавиатуру главного меню (в одну строку)
    /// </summary>
    public InlineKeyboardMarkup GetCompactKeyboard()
    {
        var keyboard = new InlineKeyboardButton[][]
        {
            // Все кнопки в одной строке
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🎒", "backpack"),
                InlineKeyboardButton.WithCallbackData("📊", "statistics"),
                InlineKeyboardButton.WithCallbackData("⚙️", "settings")
            }
        };

        return new InlineKeyboardMarkup(keyboard);
    }

    /// <summary>
    /// Возвращает клавиатуру с дополнительной кнопкой "Помощь"
    /// </summary>
    public InlineKeyboardMarkup GetKeyboardWithHelp()
    {
        return GetKeyboard();
    }
}
