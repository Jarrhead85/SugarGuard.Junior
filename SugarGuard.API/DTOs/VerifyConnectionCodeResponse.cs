namespace SugarGuard.API.DTOs;

/// <summary>
/// Ответ на проверку кода привязки
/// </summary>
public class VerifyConnectionCodeResponse
{
    public bool Success { get; set; } // Успешно ли прошла проверка

    public bool IsValid { get; set; } // Валиден ли код

    public Guid? ChildId { get; set; } // ID ребёнка

    public Guid? LinkId { get; set; } // ID созданной связи родитель-ребёнок

    public string? Message { get; set; } // Сообщени

    public string? ErrorMessage { get; set; } // Сообщение об ошибке
}
