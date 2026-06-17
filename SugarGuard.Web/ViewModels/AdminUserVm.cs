// SugarGuard.Web/ViewModels/AdminUserVm.cs
using SugarGuard.Web.Services;

namespace SugarGuard.Web.ViewModels;

/// <summary>UI-модель пользователя из Admin-раздела.</summary>
public sealed class AdminUserVm
{
    public Guid      UserId       { get; init; }
    public string    EmailForLogin { get; init; } = string.Empty;
    public long?     TelegramId   { get; init; }
    /// <summary>Parent, Doctor, Admin, SupportAdmin.</summary>
    public string    Role         { get; set; } = string.Empty;
    public bool      IsActive     { get; init; }
    public DateTime CreatedAt    { get; init; }

    /// <summary>Создаёт VM из транспортного DTO сервиса.</summary>
    internal static AdminUserVm FromDto(AdminUserResponseDto dto) => new()
    {
        UserId        = dto.UserId,
        EmailForLogin = dto.EmailForLogin,
        TelegramId    = dto.TelegramId,
        Role          = dto.Role,
        IsActive      = dto.IsActive,
        CreatedAt     = dto.CreatedAt
    };
}
