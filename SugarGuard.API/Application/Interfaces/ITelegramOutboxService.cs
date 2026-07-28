using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Управляет очередью исходящих Telegram-сообщений.
/// </summary>
public interface ITelegramOutboxService
{
    Task QueueAsync(long telegramUserId, string messageType, string text, double? latitude = null,
        double? longitude = null, bool requiresAcknowledgement = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TelegramOutboxMessageResponse>> ClaimPendingAsync(int limit, CancellationToken cancellationToken = default);

    Task MarkPartDeliveredAsync(Guid messageId, TelegramOutboxDeliveryPart part, CancellationToken cancellationToken = default);
    Task<bool> AcknowledgeAsync(Guid messageId, long telegramUserId, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid messageId, TelegramOutboxDeliveryRequest request, CancellationToken cancellationToken = default);
}
