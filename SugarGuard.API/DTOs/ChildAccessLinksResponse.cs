namespace SugarGuard.API.DTOs;

/// <summary>
/// Снимок активных связок «родитель/врач - ребёнок» для страницы управления доступом
/// </summary>
public sealed class ChildAccessLinksResponse
{
    public Guid ChildId { get; set; } // ID ребёнка, для которого запрошены связки
   
    public IReadOnlyList<LinkedAccessUserResponse> ParentLinks { get; set; } = []; // Список привязанных родителей
   
    public IReadOnlyList<LinkedAccessUserResponse> DoctorLinks { get; set; } = []; // Список привязанных врачей
}

/// <summary>
/// Данные одного участника связки для отображения в Web UI
/// </summary>
public sealed class LinkedAccessUserResponse
{   
    public Guid LinkId { get; set; } // ID записи связки в БД
   
    public Guid UserId { get; set; } // ID пользователя (родителя/врача)
   
    public string? EmailForLogin { get; set; } // Email для входа
   
    public long? TelegramId { get; set; } // Telegram ID (если задан)
   
    public string UserRole { get; set; } = string.Empty; // Роль пользователя в системе
   
    public DateTime LinkedAt { get; set; } // Дата создания связки
    public string DisplayName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? Specialty { get; set; }
}
