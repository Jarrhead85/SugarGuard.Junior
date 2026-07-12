using System.ComponentModel.DataAnnotations;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.DTOs;

public sealed class CreateSupportConversationRequest
{
    [Required, StringLength(180, MinimumLength = 3)]
    public string Subject { get; init; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 2)]
    public string Message { get; init; } = string.Empty;
}

public sealed class AddSupportMessageRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;
}

public sealed class UpdateSupportStatusRequest
{
    public SupportConversationStatus Status { get; init; }
}

public class SupportConversationDto
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

public sealed class SupportConversationDetailsDto : SupportConversationDto
{
    public IReadOnlyList<SupportMessageDto> Messages { get; init; } = Array.Empty<SupportMessageDto>();
}

public sealed class SupportMessageDto
{
    public Guid MessageId { get; init; }
    public Guid AuthorUserId { get; init; }
    public string AuthorRole { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public bool IsOwnMessage { get; init; }
}
