namespace SugarGuard.Junior.Core.Security;

/// <summary>
/// Абстракция для криптографических операций с поддержкой версионирования.
/// <para>
/// Реализации:
/// <list type="bullet">
///   <item><description><see cref="AesGcmEncryptionService"/> — основная, AES-256-GCM</description></item>
///   <item><description><see cref="LegacyAesCbcDecryptionService"/> — только decrypt AES-CBC (для чтения старых данных)</description></item>
///   <item><description><see cref="VersionedEncryptionService"/> — dispatcher, ветвит по <see cref="EncryptionVersion"/></description></item>
/// </list>
/// </para>
/// <para>
/// Формат хранения ciphertext: <c>Base64(nonce || ciphertext || tag)</c>.
/// <list type="bullet">
///   <item><description>Для GCM: nonce=12 байт, tag=16 байт</description></item>
///   <item><description>Для legacy CBC: iv=16 байт (без tag)</description></item>
/// </list>
/// </para>
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Зашифровать plaintext, возвращая пару (ciphertext, version).
    /// Всегда возвращает <see cref="EncryptionVersion.AesGcm"/> для новых записей.
    /// </summary>
    /// <param name="plaintext">Открытый текст (UTF-8).</param>
    /// <returns>Base64-encoded ciphertext + версия шифрования.</returns>
    EncryptedValue Encrypt(string plaintext);

    /// <summary>
    /// Расшифровать ciphertext указанной версии.
    /// </summary>
    /// <param name="ciphertext">Base64-encoded ciphertext из БД.</param>
    /// <param name="version">Версия шифрования, которой зашифрована запись.</param>
    /// <returns>Открытый текст (UTF-8).</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Если данные повреждены, IV/nonce неверный, или tag не прошёл проверку.
    /// </exception>
    string Decrypt(string ciphertext, EncryptionVersion version);

    /// <summary>
    /// Версия шифрования, используемая для НОВЫХ записей.
    /// </summary>
    EncryptionVersion CurrentVersion { get; }
}

/// <summary>
/// Пара (ciphertext, version) для записи в БД.
/// </summary>
public readonly record struct EncryptedValue(string Ciphertext, EncryptionVersion Version);
