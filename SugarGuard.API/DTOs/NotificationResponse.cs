namespace SugarGuard.API.DTOs;

/// <summary>
/// Ответ на запрос отправки уведомления
/// </summary>
public class NotificationResponse
{
    public bool Success { get; set; } // Успешность отправки

    public int ParentsNotified { get; set; } // Количество родителей, которым отправлено уведомление

    public string? ErrorMessage { get; set; } // Сообщение об ошибке

    public DateTime SentAt { get; set; } = DateTime.UtcNow; // Время отправки уведомления
}
