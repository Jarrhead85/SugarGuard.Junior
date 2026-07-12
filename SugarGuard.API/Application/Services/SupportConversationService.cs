using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Services;

public sealed class SupportConversationService : ISupportConversationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<SupportConversationService> _logger;

    public SupportConversationService(
        AppDbContext db,
        ICurrentUserContext currentUser,
        ILogger<SupportConversationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SupportConversationDto>> GetConversationsAsync(
        CancellationToken cancellationToken = default)
    {
        var (userId, isSupport) = GetCaller();
        var query = _db.SupportConversations.AsNoTracking();
        if (!isSupport)
        {
            query = query.Where(conversation => conversation.RequesterUserId == userId);
        }

        return await query
            .OrderBy(conversation => conversation.Status == SupportConversationStatus.Closed)
            .ThenByDescending(conversation => conversation.UpdatedAt)
            .Select(conversation => new SupportConversationDto
            {
                ConversationId = conversation.ConversationId,
                Subject = conversation.Subject,
                Status = conversation.Status,
                RequesterUserId = conversation.RequesterUserId,
                RequesterEmail = conversation.RequesterUser.EmailForLogin ?? string.Empty,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = conversation.UpdatedAt,
                LastMessagePreview = conversation.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .Select(message => message.Body.Length > 120 ? message.Body.Substring(0, 120) + "…" : message.Body)
                    .FirstOrDefault() ?? string.Empty,
                UnreadCount = conversation.Messages.Count(message =>
                    isSupport ? !message.ReadBySupport : !message.ReadByRequester)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SupportConversationDetailsDto> GetConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var (userId, isSupport) = GetCaller();
        var conversation = await GetAccessibleConversationAsync(conversationId, userId, isSupport, cancellationToken);
        return MapDetails(conversation, userId, isSupport);
    }

    public async Task<SupportConversationDetailsDto> CreateConversationAsync(
        CreateSupportConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var (userId, isSupport) = GetCaller();
        if (isSupport)
        {
            throw new InvalidOperationException("Обращение создается от имени пользователя.");
        }

        var subject = NormalizeRequired(request.Subject, 180);
        var body = NormalizeRequired(request.Message, 4000);
        var now = DateTime.UtcNow;
        var conversation = new SupportConversation
        {
            RequesterUserId = userId,
            Subject = subject,
            Status = SupportConversationStatus.WaitingForSupport,
            CreatedAt = now,
            UpdatedAt = now
        };
        var message = CreateMessage(conversation.ConversationId, userId, body, isSupport: false, now);
        conversation.Messages.Add(message);
        _db.SupportConversations.Add(conversation);
        await NotifySupportAsync(conversation, message, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Создано обращение в поддержку {ConversationId} пользователем {UserId}", conversation.ConversationId, userId);
        conversation.RequesterUser = await _db.Users.AsNoTracking().SingleAsync(user => user.UserId == userId, cancellationToken);
        message.AuthorUser = conversation.RequesterUser;
        return MapDetails(conversation, userId, false);
    }

    public async Task<SupportMessageDto> AddMessageAsync(
        Guid conversationId,
        AddSupportMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var (userId, isSupport) = GetCaller();
        var conversation = await GetAccessibleConversationAsync(conversationId, userId, isSupport, cancellationToken, tracking: true);
        if (conversation.Status == SupportConversationStatus.Closed)
        {
            throw new InvalidOperationException("Закрытое обращение нельзя дополнить.");
        }

        var now = DateTime.UtcNow;
        var message = CreateMessage(conversationId, userId, NormalizeRequired(request.Message, 4000), isSupport, now);
        _db.SupportMessages.Add(message);
        conversation.UpdatedAt = now;
        conversation.Status = isSupport
            ? SupportConversationStatus.WaitingForUser
            : SupportConversationStatus.WaitingForSupport;

        if (isSupport)
        {
            _db.UserNotifications.Add(new UserNotification
            {
                RecipientUserId = conversation.RequesterUserId,
                Type = "info",
                Title = "Ответ службы поддержки",
                Description = conversation.Subject,
                SourceType = "support_reply",
                SourceId = message.MessageId,
                CreatedAt = now
            });
        }
        else
        {
            await NotifySupportAsync(conversation, message, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new SupportMessageDto
        {
            MessageId = message.MessageId,
            AuthorUserId = userId,
            AuthorRole = _currentUser.GetRole()?.ToString() ?? string.Empty,
            Body = message.Body,
            CreatedAt = message.CreatedAt,
            IsOwnMessage = true
        };
    }

    public async Task UpdateStatusAsync(
        Guid conversationId,
        SupportConversationStatus status,
        CancellationToken cancellationToken = default)
    {
        var (userId, isSupport) = GetCaller();
        if (!isSupport)
        {
            throw new UnauthorizedAccessException("Статус обращения изменяет служба поддержки.");
        }

        if (status is not (SupportConversationStatus.Open or SupportConversationStatus.Closed))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        var conversation = await GetAccessibleConversationAsync(conversationId, userId, true, cancellationToken, tracking: true);
        conversation.Status = status;
        conversation.UpdatedAt = DateTime.UtcNow;
        conversation.ClosedAt = status == SupportConversationStatus.Closed ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkReadAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var (userId, isSupport) = GetCaller();
        await GetAccessibleConversationAsync(conversationId, userId, isSupport, cancellationToken);
        var query = _db.SupportMessages.Where(message => message.ConversationId == conversationId);
        if (isSupport)
        {
            await query.Where(message => !message.ReadBySupport)
                .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.ReadBySupport, true), cancellationToken);
        }
        else
        {
            await query.Where(message => !message.ReadByRequester)
                .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.ReadByRequester, true), cancellationToken);
        }
    }

    private async Task<SupportConversation> GetAccessibleConversationAsync(
        Guid conversationId,
        Guid userId,
        bool isSupport,
        CancellationToken cancellationToken,
        bool tracking = false)
    {
        var query = tracking ? _db.SupportConversations.AsQueryable() : _db.SupportConversations.AsNoTracking();
        query = query.Include(conversation => conversation.RequesterUser)
            .Include(conversation => conversation.Messages)
            .ThenInclude(message => message.AuthorUser);
        if (!isSupport)
        {
            query = query.Where(conversation => conversation.RequesterUserId == userId);
        }

        return await query.SingleOrDefaultAsync(conversation => conversation.ConversationId == conversationId, cancellationToken)
            ?? throw new KeyNotFoundException("Обращение не найдено.");
    }

    private async Task NotifySupportAsync(
        SupportConversation conversation,
        SupportMessage message,
        CancellationToken cancellationToken)
    {
        var recipients = await _db.Users.AsNoTracking()
            .Where(user => user.IsActive && (user.Role == UserRole.Admin || user.Role == UserRole.SupportAdmin))
            .Select(user => user.UserId)
            .ToListAsync(cancellationToken);

        foreach (var recipientId in recipients)
        {
            _db.UserNotifications.Add(new UserNotification
            {
                RecipientUserId = recipientId,
                Type = "support",
                Title = "Обращение в поддержку",
                Description = conversation.Subject,
                SourceType = "support_message",
                SourceId = message.MessageId,
                CreatedAt = message.CreatedAt
            });
        }
    }

    private static SupportMessage CreateMessage(
        Guid conversationId,
        Guid authorUserId,
        string body,
        bool isSupport,
        DateTime now) => new()
        {
            ConversationId = conversationId,
            AuthorUserId = authorUserId,
            Body = body,
            CreatedAt = now,
            ReadByRequester = !isSupport,
            ReadBySupport = isSupport
        };

    private static string NormalizeRequired(string value, int maxLength)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maxLength)
        {
            throw new ArgumentException("Некорректная длина текста.");
        }

        return normalized;
    }

    private (Guid UserId, bool IsSupport) GetCaller()
    {
        var userId = _currentUser.GetUserId() ?? throw new UnauthorizedAccessException();
        var role = _currentUser.GetRole() ?? throw new UnauthorizedAccessException();
        return (userId, role is UserRole.Admin or UserRole.SupportAdmin);
    }

    private static SupportConversationDetailsDto MapDetails(
        SupportConversation conversation,
        Guid callerUserId,
        bool isSupport) => new()
    {
        ConversationId = conversation.ConversationId,
        Subject = conversation.Subject,
        Status = conversation.Status,
        RequesterUserId = conversation.RequesterUserId,
        RequesterEmail = conversation.RequesterUser.EmailForLogin ?? string.Empty,
        CreatedAt = conversation.CreatedAt,
        UpdatedAt = conversation.UpdatedAt,
        LastMessagePreview = conversation.Messages.OrderByDescending(message => message.CreatedAt).Select(message => message.Body).FirstOrDefault() ?? string.Empty,
        UnreadCount = conversation.Messages.Count(message => isSupport ? !message.ReadBySupport : !message.ReadByRequester),
        Messages = conversation.Messages.OrderBy(message => message.CreatedAt).Select(message => new SupportMessageDto
        {
            MessageId = message.MessageId,
            AuthorUserId = message.AuthorUserId,
            AuthorRole = message.AuthorUser.Role.ToString(),
            Body = message.Body,
            CreatedAt = message.CreatedAt,
            IsOwnMessage = message.AuthorUserId == callerUserId
        }).ToArray()
    };
}
