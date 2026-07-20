namespace SugarGuard.API.DTOs;

public sealed class UserNotificationDto
{
    public Guid? NotificationId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string Type { get; init; } = "info";
    public bool IsUnread { get; init; }
    public Guid? ChildId { get; init; }
    public string SourceType { get; init; } = string.Empty;
}
