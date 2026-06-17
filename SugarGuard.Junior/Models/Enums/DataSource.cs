// Откуда пришли данные о глюкозе
namespace SugarGuard.Junior.Models.Enums;

public enum DataSource
{
    /// <summary>
    /// Ручной ввод от ребёнка
    /// </summary>
    ManualInput,

    /// <summary>
    /// Глюкометр (обычно точнее)
    /// </summary>
    Glucometer,

    /// <summary>
    /// Система непрерывного мониторинга (CGM) - FreeStyle Libre, Dexcom и т.д.
    /// </summary>
    CGMSystem,

    /// <summary>
    /// Импорт из внешних систем
    /// </summary>
    ImportedData
}
