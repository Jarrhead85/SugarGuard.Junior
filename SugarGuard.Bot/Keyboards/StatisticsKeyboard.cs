using Telegram.Bot.Types.ReplyMarkups;

namespace SugarGuard.Bot.Keyboards;

/// <summary>
/// Клавиатура для выбора периода статистики
/// Предоставляет кнопки для выбора временного диапазона: День, Неделя, Месяц, Год
/// </summary>
public class StatisticsKeyboard
{
    /// <summary>
    /// Создаёт клавиатуру выбора периода статистики
    /// </summary>
    /// <returns>Инлайн-клавиатура с кнопками периодов</returns>
    public InlineKeyboardMarkup GetPeriodSelectionKeyboard()
    {
        var buttons = new InlineKeyboardButton[][]
        {
            // Первая строка - День и Неделя
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("📅 День", "stats_day"),
                InlineKeyboardButton.WithCallbackData("📊 Неделя", "stats_week")
            },
            
            // Вторая строка - Месяц и Год
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("📈 Месяц", "stats_month"),
                InlineKeyboardButton.WithCallbackData("📋 Год", "stats_year")
            },
            
            // Третья строка - Назад в главное меню
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Главное меню", "main_menu")
            }
        };

        return new InlineKeyboardMarkup(buttons);
    }

    /// <summary>
    /// Создаёт клавиатуру для конкретного периода статистики с дополнительными действиями
    /// </summary>
    /// <param name="selectedPeriod">Выбранный период (day, week, month, year)</param>
    /// <returns>Клавиатура с действиями для выбранного периода</returns>
    public InlineKeyboardMarkup GetStatisticsActionsKeyboard(string selectedPeriod)
    {
        var periodName = selectedPeriod switch
        {
            "day" => "День",
            "week" => "Неделя", 
            "month" => "Месяц",
            "year" => "Год",
            _ => "Период"
        };

        var buttons = new InlineKeyboardButton[][]
        {
            // Первая строка - Обновить данные
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Обновить", $"refresh_stats_{selectedPeriod}")
            },
            
            // Вторая строка - Экспорт в PDF (если доступен)
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("📄 Скачать PDF", $"export_pdf_{selectedPeriod}")
            },
            
            // Третья строка - Выбрать другой период и Главное меню
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("📊 Другой период", "statistics"),
                InlineKeyboardButton.WithCallbackData("🔙 Главное меню", "main_menu")
            }
        };

        return new InlineKeyboardMarkup(buttons);
    }

    /// <summary>
    /// Создаёт компактную клавиатуру статистики (только основные периоды)
    /// </summary>
    /// <returns>Компактная клавиатура</returns>
    public InlineKeyboardMarkup GetCompactPeriodKeyboard()
    {
        var buttons = new InlineKeyboardButton[][]
        {
            // Все периоды в одной строке
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("День", "stats_day"),
                InlineKeyboardButton.WithCallbackData("Неделя", "stats_week"),
                InlineKeyboardButton.WithCallbackData("Месяц", "stats_month")
            },
            
            // Назад в главное меню
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Назад", "main_menu")
            }
        };

        return new InlineKeyboardMarkup(buttons);
    }

    /// <summary>
    /// Создаёт клавиатуру с сообщением об ошибке загрузки статистики
    /// </summary>
    /// <returns>Клавиатура с кнопками повтора и возврата</returns>
    public InlineKeyboardMarkup GetErrorKeyboard()
    {
        var buttons = new InlineKeyboardButton[][]
        {
            // Кнопки повтора и возврата
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Повторить", "statistics"),
                InlineKeyboardButton.WithCallbackData("🔙 Главное меню", "main_menu")
            }
        };

        return new InlineKeyboardMarkup(buttons);
    }

    /// <summary>
    /// Создаёт клавиатуру для навигации по дням/неделям/месяцам
    /// </summary>
    /// <param name="period">Тип периода (day, week, month)</param>
    /// <param name="canGoPrevious">Можно ли перейти к предыдущему периоду</param>
    /// <param name="canGoNext">Можно ли перейти к следующему периоду</param>
    /// <returns>Клавиатура с кнопками навигации</returns>
    public InlineKeyboardMarkup GetNavigationKeyboard(string period, bool canGoPrevious = true, bool canGoNext = true)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Кнопки навигации (если доступны)
        if (canGoPrevious || canGoNext)
        {
            var navigationRow = new List<InlineKeyboardButton>();
            
            if (canGoPrevious)
            {
                navigationRow.Add(InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"nav_prev_{period}"));
            }
            
            if (canGoNext)
            {
                navigationRow.Add(InlineKeyboardButton.WithCallbackData("➡️ Вперёд", $"nav_next_{period}"));
            }
            
            buttons.Add(navigationRow.ToArray());
        }

        // Основные действия
        buttons.Add(new InlineKeyboardButton[]
        {
            InlineKeyboardButton.WithCallbackData("🔄 Обновить", $"refresh_stats_{period}"),
            InlineKeyboardButton.WithCallbackData("📊 Период", "statistics")
        });

        // Возврат в главное меню
        buttons.Add(new InlineKeyboardButton[]
        {
            InlineKeyboardButton.WithCallbackData("🔙 Главное меню", "main_menu")
        });

        return new InlineKeyboardMarkup(buttons);
    }
}