// Настройки диабета для конкретного ребёнка
// Используется для расчётов и рекомендаций
using SugarGuard.Junior.Core.Security;

namespace SugarGuard.Junior.Models.Core;

public class DiabetesSettings
{
    /// <summary>
    /// ID профиля ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованный минимальный целевой уровень глюкозы (PHI)
    /// Типично 4.0 для детей
    /// </summary>
    public string? EncryptedTargetRangeMin { get; set; }

    /// <summary>
    /// Зашифрованный максимальный целевой уровень глюкозы (PHI)
    /// Типично 10.0 для детей
    /// </summary>
    public string? EncryptedTargetRangeMax { get; set; }

    /// <summary>
    /// Зашифрованная чувствительность к инсулину (PHI)
    /// Пример: 1 ед. инсулина снижает уровень на 1.5 ммоль/л
    /// </summary>
    public string? EncryptedInsulinSensitivity { get; set; }

    /// <summary>
    /// Зашифрованный коэффициент углеводов-инсулина (PHI)
    /// Пример: 1 ед. инсулина покрывает 10 г углеводов
    /// </summary>
    public string? EncryptedCarbInsulinRatio { get; set; }

    /// <summary>
    /// Длительность действия длительного инсулина (часов)
    /// Типично 24 часа
    /// </summary>
    public int LongActingDuration { get; set; } = 24;

    /// <summary>
    /// Длительность действия быстрого инсулина (часов)
    /// Типично 4 часа
    /// </summary>
    public int ShortActingDuration { get; set; } = 4;

    /// <summary>
    /// Дата последнего обновления
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Версия шифрования зашифрованных полей этой записи (см. <c>EncryptionVersion</c>).
    /// Используется <see cref="VersionedEncryptionService"/> для dual-decrypt CBC/GCM при миграции.
    /// </summary>
    public EncryptionVersion EncryptionVersion { get; set; } = EncryptionVersion.AesGcm;
}
