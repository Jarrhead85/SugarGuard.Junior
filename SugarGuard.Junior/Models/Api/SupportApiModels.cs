using SugarGuard.Domain.Enums;

namespace SugarGuard.Junior.Models.Api;

public class SupportConversationApiModel
{
    public Guid ConversationId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public SupportConversationStatus Status { get; set; }
    public Guid RequesterUserId { get; set; }
    public string RequesterEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string LastMessagePreview { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
}

public sealed class SupportConversationDetailsApiModel : SupportConversationApiModel
{
    public IReadOnlyList<SupportMessageApiModel> Messages { get; set; } = Array.Empty<SupportMessageApiModel>();
}

public sealed class SupportMessageApiModel
{
    public Guid MessageId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string AuthorRole { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsOwnMessage { get; set; }
}

public sealed class CreateSupportConversationApiRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
