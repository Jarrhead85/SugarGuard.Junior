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
    public string DisplayName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? Specialty { get; init; }
    public string? LicenseNumber { get; init; }
    public bool IsEmailVerified { get; init; }

    public bool IsAutomationAccount
    {
        get
        {
            var email = EmailForLogin ?? string.Empty;
            return email.Contains(".demo@", StringComparison.OrdinalIgnoreCase)
                || email.StartsWith("web-smoke-", StringComparison.OrdinalIgnoreCase)
                || email.StartsWith("codex.", StringComparison.OrdinalIgnoreCase)
                || email.StartsWith("sugarguard.mobile.", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Создаёт VM из транспортного DTO сервиса.</summary>
    internal static AdminUserVm FromDto(AdminUserResponseDto dto) => new()
    {
        UserId        = dto.UserId,
        EmailForLogin = dto.EmailForLogin,
        TelegramId    = dto.TelegramId,
        Role          = dto.Role,
        IsActive      = dto.IsActive,
        CreatedAt     = dto.CreatedAt,
        DisplayName = dto.DisplayName,
        PhotoUrl = dto.PhotoUrl,
        Specialty = dto.Specialty,
        LicenseNumber = dto.LicenseNumber,
        IsEmailVerified = dto.IsEmailVerified
    };
}
