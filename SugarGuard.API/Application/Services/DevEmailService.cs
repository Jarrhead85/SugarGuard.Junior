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
    {
        EmailValidator.ValidateOrThrow(toEmail, subject, htmlBody);

        _logger.LogWarning(
            "[DEV EMAIL] To={To} Subject={Subject} Plain={Plain} Html={Html}",
            toEmail, subject, plainTextBody, htmlBody);
        return Task.CompletedTask;
    }
}
