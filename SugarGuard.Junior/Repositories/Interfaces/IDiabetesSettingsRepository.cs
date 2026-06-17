// Интерфейс для работы с настройками диабета
using SugarGuard.Junior.Models.Core;

namespace SugarGuard.Junior.Repositories.Interfaces;

/// <summary>
/// Репозиторий для работы с настройками диабета
/// </summary>
public interface IDiabetesSettingsRepository : IRepository<DiabetesSettings>
{
    /// <summary>
    /// Получает настройки диабета для ребёнка (read-only, AsNoTracking)
    /// </summary>
    Task<DiabetesSettings?> GetByChildIdAsync(string childId);

    /// <summary>
    /// Получает настройки диабета для ребёнка для обновления (tracked)
    /// </summary>
    Task<DiabetesSettings?> GetByChildIdForUpdateAsync(string childId);

    /// <summary>
    /// Получает расшифрованный минимальный целевой диапазон
    /// </summary>
    Task<double> GetDecryptedTargetRangeMinAsync(DiabetesSettings settings);

    /// <summary>
    /// Получает расшифрованный максимальный целевой диапазон
    /// </summary>
    Task<double> GetDecryptedTargetRangeMaxAsync(DiabetesSettings settings);

    /// <summary>
    /// Получает расшифрованную чувствительность к инсулину
    /// </summary>
    Task<double> GetDecryptedInsulinSensitivityAsync(DiabetesSettings settings);

    /// <summary>
    /// Получает расшифрованный коэффициент углеводов-инсулина
    /// </summary>
    Task<double> GetDecryptedCarbInsulinRatioAsync(DiabetesSettings settings);

    /// <summary>
    /// Шифрует минимальный целевой диапазон
    /// </summary>
    Task<string> EncryptTargetRangeMinAsync(double value);

    /// <summary>
    /// Шифрует максимальный целевой диапазон
    /// </summary>
    Task<string> EncryptTargetRangeMaxAsync(double value);

    /// <summary>
    /// Шифрует чувствительность к инсулину
    /// </summary>
    Task<string> EncryptInsulinSensitivityAsync(double value);

    /// <summary>
    /// Шифрует коэффициент углеводов-инсулина
    /// </summary>
    Task<string> EncryptCarbInsulinRatioAsync(double value);

    /// <summary>
    /// Обновляет настройки диабета уже зашифрованными значениями (шифрование выполняется в вызывающем коде)
    /// </summary>
    Task UpdateEncryptedAsync(string childId, string encryptedTargetMin, string encryptedTargetMax, string encryptedSensitivity, string encryptedRatio, int longDuration, int shortDuration);

    /// <summary>
    /// Добавляет настройки диабета с уже зашифрованными значениями
    /// </summary>
    Task AddEncryptedAsync(string childId, string encryptedTargetMin, string encryptedTargetMax, string encryptedSensitivity, string encryptedRatio, int longDuration, int shortDuration);
}
