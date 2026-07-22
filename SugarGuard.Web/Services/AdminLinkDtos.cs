namespace SugarGuard.Web.Services;

/// <summary>
/// DTO связи Parent–Child из Admin API
/// </summary>
public sealed class AdminParentChildLinkDto
{   
    public Guid ParentUserId { get; init; } // Идентификатор родителя

    public string? ParentDisplayName { get; init; } // Email родителя
   
    public Guid ChildId { get; init; } // Идентификатор ребёнка

    public string? ChildDisplayName { get; init; } // Имя ребёнка
   
    public DateTime LinkedAt { get; init; } // Дата создания связи
}

/// <summary>
/// DTO связи Doctor–Child из Admin API
/// </summary>
public sealed class AdminDoctorChildLinkDto
{   
    public Guid DoctorUserId { get; init; } // Идентификатор врача

    public string? DoctorDisplayName { get; init; } // Email врача
   
    public Guid ChildId { get; init; } // Идентификатор ребёнка

    public string? ChildDisplayName { get; init; } // Имя ребёнка
   
    public DateTime LinkedAt { get; init; } // Дата создания связи
}
