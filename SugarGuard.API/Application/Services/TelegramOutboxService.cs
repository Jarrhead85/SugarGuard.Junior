using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Очередь доставки: API сохраняет событие, а бот с VPN забирает и отправляет его в Telegram.
/// </summary>
public sealed class TelegramOutboxService : ITelegramOutboxService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private const int MaxDeliveryAttempts = 10;
    private readonly AppDbContext _db;
    private readonly ILogger<TelegramOutboxService> _logger;

    public TelegramOutboxService(AppDbContext db, ILogger<TelegramOutboxService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task QueueAsync(long telegramUserId, string messageType, string text, double? latitude = null,
        double? longitude = null, bool requiresAcknowledgement = false, CancellationToken cancellationToken = default)
    {
        if (telegramUserId <= 0 || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _db.Set<TelegramOutboxMessage>().Add(new TelegramOutboxMessage
        {
            TelegramUserId = telegramUserId,
            MessageType = messageType,
            Text = text.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            RequiresAcknowledgement = requiresAcknowledgement
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelegramOutboxMessageResponse>> ClaimPendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var now = DateTime.UtcNow;
        List<TelegramOutboxMessage> messages;
        if (_db.Database.IsRelational())
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            messages = await GetMessagesForLeaseAsync(now, limit, cancellationToken);
            LeaseMessages(messages, now);
            if (messages.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            messages = await GetMessagesForLeaseAsync(now, limit, cancellationToken);
            LeaseMessages(messages, now);
            if (messages.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return messages.Select(message => new TelegramOutboxMessageResponse
        {
            MessageId = message.TelegramOutboxMessageId,
            TelegramUserId = message.TelegramUserId,
            MessageType = message.MessageType,
            Text = message.Text,
            Latitude = message.Latitude,
            Longitude = message.Longitude,
            RequiresAcknowledgement = message.RequiresAcknowledgement,
            TextDelivered = message.TextDeliveredAt is not null,
            LocationDelivered = message.LocationDeliveredAt is not null
        }).ToList();
    }

    public async Task MarkPartDeliveredAsync(
        Guid messageId,
        TelegramOutboxDeliveryPart part,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.Set<TelegramOutboxMessage>()
            .SingleOrDefaultAsync(item => item.TelegramOutboxMessageId == messageId, cancellationToken);
        if (message is null || message.DeliveredAt is not null)
        {
            return;
        }

        if (part == TelegramOutboxDeliveryPart.Text)
        {
            message.TextDeliveredAt ??= DateTime.UtcNow;
        }
        else if (part == TelegramOutboxDeliveryPart.Location && message.Latitude.HasValue && message.Longitude.HasValue)
        {
            message.LocationDeliveredAt ??= DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> AcknowledgeAsync(Guid messageId, long telegramUserId, CancellationToken cancellationToken = default)
    {
        var message = await _db.Set<TelegramOutboxMessage>()
            .SingleOrDefaultAsync(item => item.TelegramOutboxMessageId == messageId, cancellationToken);
        if (message is null || !message.RequiresAcknowledgement || message.TelegramUserId != telegramUserId)
        {
            return false;
        }

        message.AcknowledgedAt ??= DateTime.UtcNow;
        message.AcknowledgedByTelegramUserId ??= telegramUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task CompleteAsync(Guid messageId, TelegramOutboxDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        var message = await _db.Set<TelegramOutboxMessage>()
            .SingleOrDefaultAsync(item => item.TelegramOutboxMessageId == messageId, cancellationToken);
        if (message is null || message.DeliveredAt is not null)
        {
            return;
        }

        message.LockedUntil = null;
        if (request.Delivered)
        {
            message.DeliveredAt = DateTime.UtcNow;
            message.LastError = null;
        }
        else
        {
            message.LastError = string.IsNullOrWhiteSpace(request.Error) ? "Бот не подтвердил отправку." : request.Error[..Math.Min(1000, request.Error.Length)];
            if (message.DeliveryAttempts >= MaxDeliveryAttempts)
            {
                message.FailedAt = DateTime.UtcNow;
                _logger.LogError(
                    "Telegram-сообщение {MessageId} окончательно не доставлено после {Attempts} попыток.",
                    messageId,
                    message.DeliveryAttempts);
            }
            else
            {
                var delayMinutes = Math.Min(30, Math.Max(1, (int)Math.Pow(2, Math.Min(message.DeliveryAttempts, 5))));
                message.NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
                _logger.LogWarning("Telegram-сообщение {MessageId} будет повторено через {DelayMinutes} мин.", messageId, delayMinutes);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<TelegramOutboxMessage>> GetMessagesForLeaseAsync(
        DateTime now,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = _db.Set<TelegramOutboxMessage>()
            .Where(item => item.DeliveredAt == null && item.FailedAt == null && item.NextAttemptAt <= now &&
                           (item.LockedUntil == null || item.LockedUntil <= now))
            .OrderBy(item => item.CreatedAt)
            .Take(limit);

        // PostgreSQL блокирует выбранные строки до конца транзакции. Второй
        // экземпляр бота пропустит их, поэтому одно сообщение не будет выдано
        // двум отправителям одновременно.
        if (_db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            return await _db.Set<TelegramOutboxMessage>()
                .FromSqlInterpolated($"""
                    SELECT * FROM telegram_outbox_messages
                    WHERE delivered_at IS NULL
                      AND failed_at IS NULL
                      AND next_attempt_at <= {now}
                      AND (locked_until IS NULL OR locked_until <= {now})
                    ORDER BY created_at
                    LIMIT {limit}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);
        }

        // В unit-тестах используется нереляционный провайдер EF. Он не
        // поддерживает SELECT FOR UPDATE, но сохраняет поведение выборки.
        return await query.ToListAsync(cancellationToken);
    }

    private static void LeaseMessages(IEnumerable<TelegramOutboxMessage> messages, DateTime now)
    {
        foreach (var message in messages)
        {
            message.LockedUntil = now.Add(LeaseDuration);
            message.DeliveryAttempts++;
        }
    }
}
