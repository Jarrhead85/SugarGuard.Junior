using Microsoft.Extensions.Logging;

namespace SugarGuard.Junior.Core.Security;

/// <summary>
/// Диспетчер <see cref="IEncryptionService"/>, который ветвит логику по
/// <see cref="EncryptionVersion"/> записи.
/// <para>
/// Используется как ЕДИНСТВЕННАЯ точка входа из кода MAUI. Под капотом
/// делегирует либо <see cref="AesGcmEncryptionService"/>, либо
/// <see cref="LegacyAesCbcDecryptionService"/>.
/// </para>
/// <para>
/// <b>Запись:</b> всегда <see cref="EncryptionVersion.AesGcm"/> (текущая версия).
/// <b>Чтение:</b> версия берётся из БД (<c>encryption_version</c> колонка).
/// </para>
/// <para>
/// <b>Дополнительно:</b> при чтении legacy-записи (version=1) помечает
/// её в фоновом списке <see cref="PendingReEncryptIds"/> для последующего
/// re-encrypt через <c>MauiReEncryptJob</c>.
/// </para>
/// </summary>
public sealed class VersionedEncryptionService : IEncryptionService
{
    private readonly AesGcmEncryptionService _gcm;
    private readonly LegacyAesCbcDecryptionService _legacy;
    private readonly ILogger<VersionedEncryptionService> _logger;

    /// <summary>
    /// Идентификаторы записей, которые были прочитаны из legacy-CBC и должны
    /// быть перешифрованы в GCM. Заполняется при Decrypt(version=LegacyCbc).
    /// </summary>
    public HashSet<string> PendingReEncryptIds { get; } = new();

    public EncryptionVersion CurrentVersion => EncryptionVersion.AesGcm;

    public VersionedEncryptionService(
        AesGcmEncryptionService gcm,
        LegacyAesCbcDecryptionService legacy,
        ILogger<VersionedEncryptionService> logger)
    {
        _gcm = gcm;
        _legacy = legacy;
        _logger = logger;
    }

    public EncryptedValue Encrypt(string plaintext)
    {
        // Запись ВСЕГДА в текущей версии (GCM).
        return _gcm.Encrypt(plaintext);
    }

    public string Decrypt(string ciphertext, EncryptionVersion version)
    {
        return version switch
        {
            EncryptionVersion.AesGcm => _gcm.Decrypt(ciphertext, version),
            EncryptionVersion.LegacyCbc => _legacy.Decrypt(ciphertext, version),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Неизвестная версия шифрования.")
        };
    }

    /// <summary>
    /// Зарегистрировать legacy-запись для последующего re-encrypt.
    /// Вызывается из <c>MauiReEncryptJob</c>-обработчика при чтении старых данных.
    /// </summary>
    public void MarkPendingReEncrypt(string entityId)
    {
        PendingReEncryptIds.Add(entityId);
    }
}
