namespace SugarGuard.Web.Services;

/// <summary>
/// DTO связи Parent–Child из Admin API
/// </summary>
public sealed class AdminParentChildLinkDto
{   
    public Guid ParentUserId { get; init; } // Идентификатор родителя
   
    public Guid ChildId { get; init; } // Идентификатор ребёнка
   
    public DateTime? CreatedAtUtc { get; init; } // Дата создания связи
}

/// <summary>
/// DTO связи Doctor–Child из Admin API
/// </summary>
public sealed class AdminDoctorChildLinkDto
{   
    public Guid DoctorUserId { get; init; } // Идентификатор врача
   
    public Guid ChildId { get; init; } // Идентификатор ребёнка
   
    public DateTime? CreatedAtUtc { get; init; } // Дата создания связи
}
