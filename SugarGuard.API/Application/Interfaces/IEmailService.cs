namespace SugarGuard.API.Services;

/// <summary>
/// Вложение электронного письма.
/// </summary>
public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content);

/// <summary>
/// Сервис отправки электронных писем для кодов подтверждения, уведомлений и обращений в поддержку.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Отправляет письмо с HTML-содержимым на указанный адрес.
    /// </summary>
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправляет письмо с текстовым и HTML-содержимым.
    /// </summary>
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправляет письмо с текстовым и HTML-содержимым и вложениями.
    /// </summary>
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? plainTextBody,
        IReadOnlyCollection<EmailAttachment> attachments,
        CancellationToken cancellationToken = default);
}
