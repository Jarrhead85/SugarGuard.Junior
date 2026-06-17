namespace SugarGuard.Bot.Services;

/// <summary>
/// Защита от Markdown-injection (UI spoofing) в Telegram-сообщениях.
/// </summary>
public static class MarkdownSafe
{
    /// <summary>
    /// Экранирует все спецсимволы MarkdownV1, чтобы строка отображалась как plain text.
    /// </summary>
    public static string EscapeMarkdownV1(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Экранируем в обратном порядке (чтобы не задеть уже-добавленные backslashes).
        // MarkdownV1: *, _, `, [, ]
        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)  // сначала сам backslash
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }

    /// <summary>
    /// Возвращает input, ограниченный по длине (default 256) — защита от spam
    /// через длинный user input.
    /// </summary>
    public static string Truncate(string? input, int maxLength = 256)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Length <= maxLength ? input : input[..maxLength] + "…";
    }
}

