namespace SugarGuard.API.DTOs;

public sealed class UserNotificationDto
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public string Type { get; init; } = "info";
    public bool IsUnread { get; init; }
}
