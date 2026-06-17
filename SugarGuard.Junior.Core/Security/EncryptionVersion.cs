namespace SugarGuard.Junior.Core.Security;

/// <summary>
/// Версия шифрования записи в локальной БД.
/// <para>
/// Используется для поэтапной миграции с AES-256-CBC (небезопасный, без аутентификации)
/// на AES-256-GCM (аутентифицированное шифрование, защита от tampering).
/// </para>
/// <para>
/// Хранится в колонке <c>encryption_version</c> для каждой записи.
/// При чтении: <see cref="VersionedEncryptionService"/> ветвит логику по версии.
/// При записи: всегда версия <see cref="Current"/> (= GCM).
/// </para>
/// </summary>
public enum EncryptionVersion : byte
{
    /// <summary>
    /// Legacy: AES-256-CBC без аутентификации.
    /// Только для чтения существующих данных. Новые записи НЕ пишутся в этой версии.
    /// </summary>
    LegacyCbc = 1,

    /// <summary>
    /// Текущая версия: AES-256-GCM с 12-байтным nonce и 16-байтным tag.
    /// </summary>
    AesGcm = 2
}
