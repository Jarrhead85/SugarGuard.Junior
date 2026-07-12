using SugarGuard.Domain.Enums;

namespace SugarGuard.Web.ViewModels;

public class SupportConversationVm
{
    public Guid ConversationId { get; init; }
    public string Subject { get; init; } = string.Empty;
    public SupportConversationStatus Status { get; init; }
    public Guid RequesterUserId { get; init; }
    public string RequesterEmail { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string LastMessagePreview { get; init; } = string.Empty;
    public int UnreadCount { get; init; }
}

public sealed class SupportConversationDetailsVm : SupportConversationVm
{
    public IReadOnlyList<SupportMessageVm> Messages { get; init; } = Array.Empty<SupportMessageVm>();
}

public sealed class SupportMessageVm
{
    public Guid MessageId { get; init; }
    public Guid AuthorUserId { get; init; }
    public string AuthorRole { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public bool IsOwnMessage { get; init; }
}

public sealed class CreateSupportConversationVm
{
    public string Subject { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
