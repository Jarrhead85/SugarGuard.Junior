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
            // Первая строка - Рюкзак
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🎒 Рюкзак", "backpack")
            },
            
            // Вторая строка - Статистика
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("📊 Статистика", "statistics")
            },
            
            // Третья строка - Настройки
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("⚙️ Настройки", "settings")
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
        var keyboard = new InlineKeyboardButton[][]
        {
            // Первая строка - Рюкзак и Статистика
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🎒 Рюкзак", "backpack"),
                InlineKeyboardButton.WithCallbackData("📊 Статистика", "statistics")
            },
            
            // Вторая строка - Настройки и Помощь
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("⚙️ Настройки", "settings"),
                InlineKeyboardButton.WithCallbackData("❓ Помощь", "help")
            }
        };

        return new InlineKeyboardMarkup(keyboard);
    }
}