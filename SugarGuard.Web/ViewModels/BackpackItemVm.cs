// DTO (View Model) для одного перекуса из рюкзака ребёнка.
// Используется при десериализации ответа API GET /api/backpack/{childId}
// и передаётся как параметр в Razor-компонент BackpackItemCard.
//
// Соответствует доменной модели BackpackItem (таблица backpackitems):
//   BackpackItemId  — уникальный идентификатор записи
//   ChildId         — идентификатор ребёнка-владельца
//   SnackName       — название перекуса (до 500 символов)
//   BreadUnits      — хлебные единицы (decimal, формат 4,2)
//   AddedBy         — кем добавлен: "parent" / "doctor" / имя
//   CreatedAt       — дата и время добавления (UTC)

namespace SugarGuard.Web.ViewModels;

/// <summary>
/// View Model перекуса из рюкзака ребёнка.
/// Получается из API и передаётся в компонент BackpackItemCard.
/// </summary>
public sealed class BackpackItemVm
{
    /// <summary>
    /// Уникальный идентификатор перекуса.
    /// Используется при вызовах /consume и DELETE /api/backpack/{id}.
    /// </summary>
    public Guid BackpackItemId { get; set; }

    /// <summary>
    /// Идентификатор ребёнка, которому принадлежит перекус.
    /// </summary>
    public Guid ChildId { get; set; }

    /// <summary>
    /// Название перекуса (максимум 500 символов).
    /// Отображается как заголовок карточки.
    /// </summary>
    public string SnackName { get; set; } = string.Empty;

    /// <summary>
    /// Количество хлебных единиц (ХЕ).
    /// Ключевой медицинский показатель — влияет на дозу инсулина.
    /// Формат: decimal(4,2), например 1.50.
    /// </summary>
    public decimal BreadUnits { get; set; }

    /// <summary>
    /// Кем добавлен перекус.
    /// Возможные значения: "parent", "doctor", "child" или имя пользователя.
    /// Компонент BackpackItemCard переводит "parent"/"doctor" на русский.
    /// </summary>
    public string? AddedBy { get; set; }

    /// <summary>
    /// Дата и время добавления перекуса в формате UTC.
    /// Компонент переводит в локальное время при отображении.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
