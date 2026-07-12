namespace SugarGuard.Web.ViewModels;

public sealed class UserNotificationItem
{
    public Guid? NotificationId { get; init; }
    public string Title { get; init; }
    public string Description { get; init; }
    public string Time { get; init; }
    public DateTime CreatedAt { get; init; }
    public string Type { get; init; }
    public bool IsUnread { get; set; }

    public UserNotificationItem(
        string title,
        string desc,
        string time,
        string type,
        bool isUnread,
        Guid? notificationId = null,
        DateTime createdAt = default)
    {
        Title = title; Description = desc; Time = time;
        CreatedAt = createdAt == default ? DateTime.MinValue : createdAt;
        Type = type; IsUnread = isUnread; NotificationId = notificationId;
    }
}
