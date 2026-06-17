namespace SugarGuard.API.Application.Services;

/// <summary>
/// Централизованная валидация параметров
/// </summary>
public static class EmailValidator
{
    /// <summary>
    /// Проверяет обязательные параметры
    /// </summary>
    public static void ValidateOrThrow(
        string? toEmail,
        string? subject,
        string? htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException(
                "Адрес получателя не может быть пустым.", nameof(toEmail));

        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException(
                "Тема письма не может быть пустой.", nameof(subject));

        if (string.IsNullOrWhiteSpace(htmlBody))
            throw new ArgumentException(
                "Тело письма не может быть пустым.", nameof(htmlBody));
    }
}
