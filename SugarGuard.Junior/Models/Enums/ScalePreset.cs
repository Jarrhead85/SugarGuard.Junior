namespace SugarGuard.Junior.Models.Enums;

/// <summary>
/// Пресет масштаба интерфейса (Child Mode).
/// </summary>
public enum ScalePreset
{
    /// <summary>
    /// Мелкий масштаб — для опытных пользователей, помещается больше данных.
    /// </summary>
    Small = -1,

    /// <summary>
    /// Стандартный масштаб (по умолчанию).
    /// </summary>
    Default = 0,

    /// <summary>
    /// Крупный масштаб — child-friendly, крупные кнопки и текст.
    /// </summary>
    Large = 1,
}
