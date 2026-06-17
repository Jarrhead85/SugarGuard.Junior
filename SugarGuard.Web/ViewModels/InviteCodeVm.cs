using SugarGuard.Domain.Enums;

// SugarGuard.Web/ViewModels/InviteCodeVm.cs
namespace SugarGuard.Web.ViewModels;

/// <summary>Инвайт-код — UI-модель для страниц Parent и ChildSelector.</summary>
public sealed class InviteCodeVm
{
    public Guid      InviteCodeId { get; init; }
    public Guid      ChildId      { get; init; }
    public string    Code         { get; init; } = string.Empty;
    /// <summary>Целевая роль: Parent, Doctor.</summary>
    public UserRole   TargetRole   { get; init; }
    /// <summary>Статус: active, expired, claimed.</summary>
    public string    Status       { get; init; } = string.Empty;
    public bool      IsActive     { get; init; }
    /// <summary>null — если кода нет или истёк.</summary>
    public DateTime? ExpiresAt    { get; init; }
    public DateTime  CreatedAt    { get; init; }
}
