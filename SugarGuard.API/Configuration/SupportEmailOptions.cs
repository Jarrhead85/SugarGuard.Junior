namespace SugarGuard.API.Configuration;

/// <summary>
/// Настройки email-канала службы поддержки.
/// </summary>
public sealed class SupportEmailOptions
{
    /// <summary>
    /// Имя секции конфигурации.
    /// </summary>
    public const string SectionName = "SupportEmail";

    /// <summary>
    /// Адрес ящика, на который отправляются обращения пользователей.
    /// </summary>
    public string InboxEmail { get; init; } = "sugar-guard@yandex.ru";

    /// <summary>
    /// Максимальный размер пользовательского вложения в байтах.
    /// </summary>
    public long MaxAttachmentBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>
    /// Максимальный размер диагностического файла с логами в байтах.
    /// </summary>
    public long MaxDiagnosticsBytes { get; init; } = 512 * 1024;
}
