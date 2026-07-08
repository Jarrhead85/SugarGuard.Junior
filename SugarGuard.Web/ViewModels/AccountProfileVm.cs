namespace SugarGuard.Web.ViewModels;

public sealed class AccountProfileVm
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? Specialty { get; init; }
    public string? LicenseNumber { get; init; }
}

public sealed class UpdateAccountProfileVmRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Specialty { get; init; }
    public string? LicenseNumber { get; init; }
}
