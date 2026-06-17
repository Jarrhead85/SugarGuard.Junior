// Модель пользователя (родителя)
using SugarGuard.Junior.Core.Security;

namespace SugarGuard.Junior.Models.Core;

public class User
{
    /// <summary>
    /// Уникальный идентификатор пользователя
    /// </summary>
    public string UserId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Фамилия
    /// </summary>
    public string EncryptedLastName { get; set; } = string.Empty;

    /// <summary>
    /// Имя
    /// </summary>
    public string EncryptedFirstName { get; set; } = string.Empty;

    /// <summary>
    /// Email - используется для входа и восстановления пароля
    /// </summary>
    public string EncryptedEmail { get; set; } = string.Empty;

    /// <summary>
    /// Номер телефона в формате +7 XXX XXX-XX-XX
    /// </summary>
    public string EncryptedPhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Хешированный пароль (НИКОГДА не храним пароль в открытом виде!)
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Соль для хеширования пароля
    /// Каждый пароль получает уникальную соль для безопасности
    /// </summary>
    public string PasswordSalt { get; set; } = string.Empty;

    /// <summary>
    /// Верифицирован ли email
    /// </summary>
    public bool IsEmailVerified { get; set; } = false;

    /// <summary>
    /// Телеграм ID родителя для отправки уведомлений
    /// </summary>
    public long? TelegramUserId { get; set; }

    /// <summary>
    /// Активирована ли интеграция с Телеграм
    /// </summary>
    public bool IsTelegramConnected { get; set; } = false;

    /// <summary>
    /// Дата создания аккаунта
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата последнего входа
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Версия шифрования зашифрованных полей этой записи (см. <c>EncryptionVersion</c>).
    /// Используется <see cref="VersionedEncryptionService"/> для dual-decrypt CBC/GCM при миграции.
    /// <para>
    /// При INSERT всегда <see cref="EncryptionVersion.AesGcm"/> (текущая).
    /// </para>
    /// </summary>
    public EncryptionVersion EncryptionVersion { get; set; } = EncryptionVersion.AesGcm;

    /// <summary>
    /// Полная ФИ для отображения в UI (требует дешифрования)
    /// Используйте UserRepository.GetFullNameAsync() для получения расшифрованного имени
    /// </summary>
    [Obsolete("Используйте UserRepository.GetFullNameAsync() для получения расшифрованного имени")]
    public string FullName => "*** ЗАШИФРОВАНО ***";
}
