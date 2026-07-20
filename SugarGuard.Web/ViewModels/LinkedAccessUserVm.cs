// SugarGuard.Web/ViewModels/LinkedAccessUserVm.cs
// Web UI — для страницы Parent/Links.razor

namespace SugarGuard.Web.ViewModels;

/// <summary>Пользователь, связанный с ребёнком (родитель или врач).</summary>
public sealed class LinkedAccessUserVm
{
    /// <summary>Идентификатор связи.</summary>
    public Guid    LinkId       { get; init; }
    /// <summary>Идентификатор пользователя.</summary>
    public Guid    UserId       { get; init; }
    /// <summary>Email пользователя.</summary>
    public string? EmailForLogin { get; init; }
    /// <summary>Telegram ID, если привязан.</summary>
    public long?   TelegramId   { get; init; }
    /// <summary>MAX ID, если привязан.</summary>
    public long?   MaxUserId    { get; init; }
    /// <summary>Роль: Parent, Doctor.</summary>
    public string  UserRole     { get; init; } = string.Empty;
    /// <summary>Дата создания связи (UTC).</summary>
    public DateTime LinkedAt   { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? Specialty { get; init; }
}

/// <summary>Все связи ребёнка (родители и врачи).</summary>
public sealed class ChildAccessLinksVm
{
    public List<LinkedAccessUserVm> ParentLinks { get; init; } = new();
    public List<LinkedAccessUserVm> DoctorLinks { get; init; } = new();
}
