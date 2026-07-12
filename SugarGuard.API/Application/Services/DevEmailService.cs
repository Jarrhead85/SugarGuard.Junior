using SugarGuard.API.Application.Services;

namespace SugarGuard.API.Services;

/// <summary>
/// Dev-реализация отправки писем
/// </summary>
public sealed class DevEmailService : IEmailService
{
    private readonly ILogger<DevEmailService> _logger;

    public DevEmailService(ILogger<DevEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {

        EmailValidator.ValidateOrThrow(toEmail, subject, htmlBody);

        _logger.LogWarning(
            "[DEV EMAIL] To={To} Subject={Subject} Body={Body}",
            toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }

    public Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken cancellationToken = default)
        => SendAsync(toEmail, subject, htmlBody, plainTextBody, Array.Empty<EmailAttachment>(), cancellationToken);

    public Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? plainTextBody,
        IReadOnlyCollection<EmailAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        EmailValidator.ValidateOrThrow(toEmail, subject, htmlBody);

        _logger.LogWarning(
            "[DEV EMAIL] To={To} Subject={Subject} Attachments={AttachmentCount} Plain={Plain} Html={Html}",
            toEmail, subject, attachments.Count, plainTextBody, htmlBody);
        return Task.CompletedTask;
    }
}
