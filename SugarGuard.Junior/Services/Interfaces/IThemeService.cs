// Интерфейс сервиса управления темой и масштабом интерфейса
namespace SugarGuard.Junior.Services.Interfaces;

using SugarGuard.Junior.Models.Enums;

public interface IThemeService
{
    /// <summary>
    /// Применяет пресет масштаба интерфейса (Child Mode).
    /// Пересчитывает DynamicResource-токены: шрифты, отступы, размеры.
    /// </summary>
    void ApplyScale(ScalePreset preset);

    /// <summary>
    /// Текущий активный пресет масштаба.
    /// </summary>
    ScalePreset CurrentScale { get; }

    /// <summary>
    /// Коэффициент масштабирования относительно Default (1.0).
    /// Small = 0.85, Large = 1.3.
    /// </summary>
    double GetScaleFactor();
}
