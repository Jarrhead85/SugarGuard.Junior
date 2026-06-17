namespace SugarGuard.Web.ViewModels;

public sealed class UserNotificationItem
{
    public string Title { get; init; }
    public string Description { get; init; }
    public string Time { get; init; }
    public string Type { get; init; }
    public bool IsUnread { get; set; }

    public UserNotificationItem(string title, string desc, string time, string type, bool isUnread)
    {
        Title = title; Description = desc; Time = time;
        Type = type; IsUnread = isUnread;
    }
}
