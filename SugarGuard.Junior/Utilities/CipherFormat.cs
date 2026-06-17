namespace SugarGuard.Junior.Utilities;

/// <summary>
/// Хелпер для определения формата ciphertext в локальной БД.
/// <para>
/// После миграции на AES-GCM (фикс 2026-06-03) ciphertext имеет префикс
/// версии: <c>"1:&lt;base64&gt;"</c> для legacy-CBC и <c>"2:&lt;base64&gt;"</c> для GCM.
/// </para>
/// <para>
/// До миграции ciphertext имел формат <c>base64(IV + ciphertext)</c> без префикса,
/// и старый код определял "уже зашифровано" через <c>Contains('=')</c> (base64 padding).
/// Этот хелпер заменяет heuristic на точный prefix check.
/// </para>
/// </summary>
public static class CipherFormat
{
    private const string LegacyCbcPrefix = "1:";
    private const string AesGcmPrefix = "2:";

    /// <summary>
    /// Возвращает <c>true</c>, если <paramref name="value"/> уже зашифровано
    /// (либо legacy-CBC без префикса, либо новый CBC/GCM с префиксом версии).
    /// </summary>
    public static bool IsEncrypted(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        // Новый формат: префикс "1:" или "2:".
        if (value.StartsWith(LegacyCbcPrefix, StringComparison.Ordinal) ||
            value.StartsWith(AesGcmPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        // Legacy heuristic: base64 всегда кончается на "=" или "==".
        // НО: Plain text "Иван=" теоретически может содержать '='. В нашем
        // домене имена/email не содержат '=', так что heuristic работает.
        // Тем не менее, для миграции предпочитаем префикс — он точный.
        return value.Contains('=') && value.Length >= 16;
    }

    /// <summary>
    /// Возвращает <c>true</c>, если <paramref name="value"/> имеет новый
    /// versioned-префикс (мигрировано в v1.0.0+).
    /// <para>
    /// Требует минимум 4 символа (<c>"1:x"</c> или <c>"2:x"</c>) — иначе это
    /// невалидный ciphertext и префикс считается отсутствующим.
    /// </para>
    /// </summary>
    public static bool HasVersionPrefix(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 4) return false;
        if (value[0] != '1' && value[0] != '2') return false;
        return value[1] == ':';
    }
}

