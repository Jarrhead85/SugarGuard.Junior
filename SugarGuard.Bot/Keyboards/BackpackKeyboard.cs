using Telegram.Bot.Types.ReplyMarkups;

namespace SugarGuard.Bot.Keyboards;

/// <summary>
/// Клавиатура для управления рюкзаком ребёнка
/// Отображает список перекусов с кнопками удаления и кнопку добавления
/// </summary>
public class BackpackKeyboard
{
    /// <summary>
    /// Создаёт клавиатуру рюкзака с перекусами и кнопками управления
    /// </summary>
    /// <param name="snacks">Список перекусов в рюкзаке</param>
    /// <returns>Инлайн-клавиатура для управления рюкзаком</returns>
    public InlineKeyboardMarkup GetBackpackKeyboard(List<BackpackSnack> snacks)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Добавляем кнопки для каждого перекуса
        foreach (var snack in snacks)
        {
            // Строка с названием перекуса и кнопкой удаления
            var snackRow = new InlineKeyboardButton[]
            {
                // Кнопка с названием и ХЕ (неактивная)
                InlineKeyboardButton.WithCallbackData(
                    $"{snack.SnackName} ({snack.BreadUnits} ХЕ)", 
                    $"snack_info_{snack.BackpackItemId}"),
                
                // Кнопка удаления
                InlineKeyboardButton.WithCallbackData(
                    "🗑️", 
                    $"delete_snack_{snack.BackpackItemId}")
            };
            
            buttons.Add(snackRow);
        }

        // Если рюкзак пустой, показываем сообщение
        if (!snacks.Any())
        {
            buttons.Add(new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("📭 Рюкзак пуст", "empty_backpack")
            });
        }

        // Добавляем кнопку "Добавить перекус"
        buttons.Add(new InlineKeyboardButton[]
        {
            InlineKeyboardButton.WithCallbackData("➕ Добавить перекус", "add_snack")
        });

        // Добавляем кнопку "Назад в главное меню"
        buttons.Add(new InlineKeyboardButton[]
        {
            InlineKeyboardButton.WithCallbackData("🔙 Главное меню", "main_menu")
        });

        return new InlineKeyboardMarkup(buttons);
    }

    /// <summary>
    /// Создаёт клавиатуру подтверждения удаления перекуса
    /// </summary>
    /// <param name="snackName">Название перекуса</param>
    /// <param name="backpackItemId">ID перекуса в рюкзаке</param>
    /// <returns>Клавиатура с кнопками подтверждения</returns>
    public InlineKeyboardMarkup GetDeleteConfirmationKeyboard(string snackName, Guid backpackItemId)
    {
        var buttons = new InlineKeyboardButton[][]
        {
            // Кнопки подтверждения и отмены
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Да, удалить", $"confirm_delete_{backpackItemId}"),
                InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel_delete")
            }
        };

        return new InlineKeyboardMarkup(buttons);
    }

    /// <summary>
    /// Создаёт клавиатуру для отмены добавления перекуса
    /// </summary>
    /// <returns>Клавиатура с кнопкой отмены</returns>
    public InlineKeyboardMarkup GetCancelAddKeyboard()
    {
        var buttons = new InlineKeyboardButton[][]
        {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Отменить добавление", "cancel_add_snack")
            }
        };

        return new InlineKeyboardMarkup(buttons);
    }

    /// <summary>
    /// Создаёт компактную клавиатуру рюкзака (только основные действия)
    /// </summary>
    /// <returns>Компактная клавиатура</returns>
    public InlineKeyboardMarkup GetCompactBackpackKeyboard()
    {
        var buttons = new InlineKeyboardButton[][]
        {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🎒 Показать рюкзак", "show_backpack"),
                InlineKeyboardButton.WithCallbackData("➕ Добавить", "add_snack")
            },
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Главное меню", "main_menu")
            }
        };

        return new InlineKeyboardMarkup(buttons);
    }
}

/// <summary>
/// Модель перекуса для отображения в клавиатуре
/// </summary>
public class BackpackSnack
{
    public Guid BackpackItemId { get; set; }
    public string SnackName { get; set; } = string.Empty;
    public decimal BreadUnits { get; set; }
    public DateTime CreatedAt { get; set; }
}