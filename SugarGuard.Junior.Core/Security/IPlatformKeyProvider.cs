namespace SugarGuard.Junior.Core.Security;

/// <summary>
/// Платформо-зависимый поставщик мастер-ключа AES-256.
/// <para>
/// Ключ должен:
/// <list type="bullet">
///   <item><description>быть длиной ровно 32 байта (AES-256)</description></item>
///   <item><description>генерироваться один раз при первом запуске приложения</description></item>
///   <item><description>храниться в защищённом хранилище ОС (KeyStore / Keychain / DPAPI)</description></item>
///   <item><description>НЕ покидать устройство (non-extractable в production)</description></item>
/// </list>
/// </para>
/// <para>
/// Реализации:
/// <list type="bullet">
///   <item><description><b>Android:</b> <c>AndroidKeyStoreKeyProvider</c> (TODO, требует MAUI build)</description></item>
///   <item><description><b>iOS/macOS:</b> <c>KeychainKeyProvider</c> (TODO)</description></item>
///   <item><description><b>Windows:</b> <c>DPAPIKeyProvider</c> (TODO)</description></item>
///   <item><description><b>Test:</b> <see cref="InMemoryPlatformKeyProvider"/></description></item>
/// </list>
/// </para>
/// </summary>
public interface IPlatformKeyProvider
{
    /// <summary>
    /// Получить мастер-ключ AES-256 (32 байта), создав при первом вызове.
    /// Потокобезопасно.
    /// </summary>
    byte[] GetOrCreateKey();
}
