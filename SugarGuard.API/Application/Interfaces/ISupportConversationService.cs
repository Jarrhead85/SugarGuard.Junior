using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Interfaces;

public interface ISupportConversationService
{
    Task<IReadOnlyList<SupportConversationDto>> GetConversationsAsync(CancellationToken cancellationToken = default);
    Task<SupportConversationDetailsDto> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<SupportConversationDetailsDto> CreateConversationAsync(CreateSupportConversationRequest request, CancellationToken cancellationToken = default);
    Task<SupportMessageDto> AddMessageAsync(Guid conversationId, AddSupportMessageRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid conversationId, SupportConversationStatus status, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
