using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SugarGuard.API.Application.Services;

namespace SugarGuard.API.Services
{
    /// <summary>
    /// Реализация отправки писем
    /// </summary>

    public class SmtpEmailService : IEmailService
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(
            IConfiguration configuration,
            ILogger<SmtpEmailService> logger)
        {
            _logger = logger;
            _settings = configuration
                            .GetSection("Smtp")
                            .Get<SmtpSettings>()
                        ?? throw new InvalidOperationException(
                            "Отсутствует конфигурация секции 'Smtp' в appsettings.json.");
        }

        /// <inheritdoc/>
        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
            => SendAsync(toEmail, subject, htmlBody, null, cancellationToken);

        /// <inheritdoc/>
        public async Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string? plainTextBody,
            CancellationToken cancellationToken = default)
        {
            EmailValidator.ValidateOrThrow(toEmail, subject, htmlBody);

            var message = BuildMessage(toEmail, subject, htmlBody, plainTextBody);
            using var client = new SmtpClient
            {
                Timeout = _settings.TimeoutMs
            };

            try
            {
                _logger.LogInformation(
                    "Отправка письма. To={To} Subject={Subject} SmtpHost={Host}:{Port}",
                    toEmail, subject, _settings.Host, _settings.Port);

                await client.ConnectAsync(
                    _settings.Host,
                    _settings.Port,
                    GetSecureSocketOptions(),
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(_settings.Username)
                    && !string.IsNullOrWhiteSpace(_settings.Password))
                {
                    await client.AuthenticateAsync(
                        _settings.Username,
                        _settings.Password,
                        cancellationToken);
                }

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(quit: true, cancellationToken);

                _logger.LogInformation(
                    "Письмо успешно отправлено. To={To} Subject={Subject}", toEmail, subject);
            }
            catch (MailKit.Net.Smtp.SmtpCommandException ex)
            {
                _logger.LogError(ex,
                    "SMTP-ошибка при отправке письма. To={To} StatusCode={Code} ErrorCode={ErrorCode}",
                    toEmail, ex.StatusCode, ex.ErrorCode);
                throw;
            }
            catch (MailKit.Net.Smtp.SmtpProtocolException ex)
            {
                _logger.LogError(ex,
                    "SMTP-протокольная ошибка при отправке письма. To={To}",
                    toEmail);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Непредвиденная ошибка при отправке письма. To={To}", toEmail);
                throw;
            }
        }

        // Приватные вспомогательные методы
        /// <summary>
        /// Создаёт и настраивает сообщение
        /// </summary>
        private MimeMessage BuildMessage(
            string toEmail,
            string subject,
            string htmlBody,
            string? plainTextBody)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromDisplayName, _settings.FromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var body = new BodyBuilder
            {
                HtmlBody = htmlBody
            };

            if (!string.IsNullOrWhiteSpace(plainTextBody))
            {
                body.TextBody = plainTextBody;
            }

            message.Body = body.ToMessageBody();
            return message;
        }

        private SecureSocketOptions GetSecureSocketOptions() =>
            !_settings.EnableSsl
                ? SecureSocketOptions.None
                : _settings.Port == 465
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

        // Вложенный класс конфигурации
        /// <summary>
        /// Параметры SMTP-соединения
        /// </summary>
        private sealed class SmtpSettings
        {
            public string Host { get; init; } = "localhost"; // Хост SMTP-сервера
           
            public int Port { get; init; } = 587; // Порт SMTP-сервера
           
            public bool EnableSsl { get; init; } = true; // Использовать ли SSL
           
            public string? Username { get; init; } // Логин для аутентификации на SMTP
           
            public string? Password { get; init; } // Пароль для аутентификации на SMTP
           
            public string FromAddress { get; init; } = "noreply@sugarguard.ru"; // Адрес отправителя
           
            public string FromDisplayName { get; init; } = "SugarGuard"; // Отображаемое имя отправителя
           
            public int TimeoutMs { get; init; } = 10_000; // Таймаут SMTP-соединения в миллисекундах. По умолчанию 10 секунд
        }
    }
}
