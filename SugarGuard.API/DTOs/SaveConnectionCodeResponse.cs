namespace SugarGuard.API.DTOs;

/// <summary>
/// Ответ на сохранение хеша кода привязки
/// </summary>
public class SaveConnectionCodeResponse
{
    public bool Success { get; set; } // Успешно ли сохранён хеш

    public Guid? CodeId { get; set; } // ID созданной записи кода

    public DateTime ExpiresAt { get; set; } // Время истечения кода

    public string? ErrorMessage { get; set; } // Сообщение об ошибке
}
