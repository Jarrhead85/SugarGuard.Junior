namespace SugarGuard.API.Services
{
    /// <summary>
    /// Сервис отправки электронных писем, для отправки кодов подтверждения, 
    /// а также уведомлений о смене пароля и критических событиях безопасности
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Отправляет письмо с HTML-содержимым на указанный адрес
        /// </summary>
        Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Отправляет письмо с текстовым и HTML-содержимым
        /// </summary>
        Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string plainTextBody,
            CancellationToken cancellationToken = default);
    }
}
