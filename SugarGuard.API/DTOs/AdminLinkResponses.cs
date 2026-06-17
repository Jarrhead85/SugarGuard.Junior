namespace SugarGuard.API.DTOs;

/// <summary>
/// Запись о Parent–Child связи для админ-страницы
/// </summary>
public sealed class AdminParentChildLinkResponse
{
    public Guid ParentUserId { get; set; } // ID пользователя-родителя

    public string? ParentDisplayName { get; set; } // Имя родителя

    public Guid ChildId { get; set; } // ID ребёнка

    public string? ChildDisplayName { get; set; } // Имя ребёнка

    public DateTime LinkedAt { get; set; } // Когда была создана связь

    public string? CreatedBy { get; set; } // Создал связь
}

/// <summary>
/// Запись о Doctor–Child связи для админ-страницы
/// </summary>
public sealed class AdminDoctorChildLinkResponse
{
    public Guid DoctorUserId { get; set; } // ID пользователя-врача
   
    public string? DoctorDisplayName { get; set; } // Имя врача
   
    public Guid ChildId { get; set; } // ID ребёнка
   
    public string? ChildDisplayName { get; set; } // Имя ребёнка
   
    public DateTime LinkedAt { get; set; } // Когда была создана связь
   
    public string? CreatedBy { get; set; } // Создал связь
}
