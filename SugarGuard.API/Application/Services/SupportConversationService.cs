using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Configuration;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Сервис обращений в поддержку и email-уведомлений администраторов.
/// </summary>
public sealed class SupportConversationService : ISupportConversationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IEmailService _emailService;
    private readonly SupportEmailOptions _supportEmailOptions;
    private readonly ILogger<SupportConversationService> _logger;

    /// <summary>
    /// Создаёт сервис обращений в поддержку.
    /// </summary>
    public SupportConversationService(
        AppDbContext db,
        ICurrentUserContext currentUser,
        IEmailService emailService,
        IOptions<SupportEmailOptions> supportEmailOptions,
        ILogger<SupportConversationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _emailService = emailService;
        _supportEmailOptions = supportEmailOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
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
                CallbackEmail = conversation.CallbackEmail,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = conversation.UpdatedAt,
                LastMessagePreview = conversation.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .Select(message => message.Body.Length > 120 ? message.Body.Substring(0, 120) + "..." : message.Body)
                    .FirstOrDefault() ?? string.Empty,
                UnreadCount = conversation.Messages.Count(message =>
                    isSupport ? !message.ReadBySupport : !message.ReadByRequester)
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SupportConversationDetailsDto> GetConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var (userId, isSupport) = GetCaller();
        var conversation = await GetAccessibleConversationAsync(conversationId, userId, isSupport, cancellationToken);
        return MapDetails(conversation, userId, isSupport);
    }

    /// <inheritdoc/>
    public async Task<SupportConversationDetailsDto> CreateConversationAsync(
        CreateSupportConversationRequest request,
        CancellationToken cancellationToken = default)
        => await CreateConversationCoreAsync(
            request.Subject,
            request.Message,
            request.CallbackEmail,
            clientLogs: null,
            attachment: null,
            cancellationToken);

    /// <inheritdoc/>
    public async Task<SupportConversationDetailsDto> CreateEmailRequestAsync(
        CreateSupportEmailRequest request,
        CancellationToken cancellationToken = default)
        => await CreateConversationCoreAsync(
            request.Subject,
            request.Message,
            request.CallbackEmail,
            request.ClientLogs,
            request.Attachment,
            cancellationToken);

    /// <inheritdoc/>
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
            await SendSupportReplyEmailAsync(conversation, message, cancellationToken);
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    private async Task<SupportConversationDetailsDto> CreateConversationCoreAsync(
        string requestSubject,
        string requestMessage,
        string? callbackEmail,
        string? clientLogs,
        IFormFile? attachment,
        CancellationToken cancellationToken)
    {
        var (userId, isSupport) = GetCaller();
        if (isSupport)
        {
            throw new InvalidOperationException("Обращение создаётся от имени пользователя.");
        }

        var subject = NormalizeRequired(requestSubject, 180);
        var body = NormalizeRequired(requestMessage, 4000);
        var normalizedCallbackEmail = NormalizeEmail(callbackEmail);
        var now = DateTime.UtcNow;
        var conversation = new SupportConversation
        {
            RequesterUserId = userId,
            Subject = subject,
            CallbackEmail = normalizedCallbackEmail,
            Status = SupportConversationStatus.WaitingForSupport,
            CreatedAt = now,
            UpdatedAt = now
        };
        var message = CreateMessage(conversation.ConversationId, userId, body, isSupport: false, now);
        conversation.Messages.Add(message);
        _db.SupportConversations.Add(conversation);

        var requester = await _db.Users
            .AsNoTracking()
            .SingleAsync(user => user.UserId == userId, cancellationToken);
        var attachments = await BuildSupportEmailAttachmentsAsync(attachment, clientLogs, cancellationToken);
        await NotifySupportAsync(conversation, message, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await SendSupportEmailAsync(conversation, message, requester.EmailForLogin, attachments, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не удалось отправить email-уведомление по обращению в поддержку. ConversationId={ConversationId}",
                conversation.ConversationId);
        }

        _logger.LogInformation(
            "Создано обращение в поддержку {ConversationId} пользователем {UserId}",
            conversation.ConversationId,
            userId);
        conversation.RequesterUser = requester;
        message.AuthorUser = requester;
        return MapDetails(conversation, userId, false);
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

    private async Task<IReadOnlyCollection<EmailAttachment>> BuildSupportEmailAttachmentsAsync(
        IFormFile? attachment,
        string? clientLogs,
        CancellationToken cancellationToken)
    {
        var attachments = new List<EmailAttachment>();

        if (attachment is not null)
        {
            if (attachment.Length > _supportEmailOptions.MaxAttachmentBytes)
            {
                throw new ArgumentException(
                    $"Размер вложения не должен превышать {_supportEmailOptions.MaxAttachmentBytes / 1024 / 1024} МБ.");
            }

            await using var stream = attachment.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            attachments.Add(new EmailAttachment(
                Path.GetFileName(attachment.FileName),
                string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType,
                memory.ToArray()));
        }

        if (!string.IsNullOrWhiteSpace(clientLogs))
        {
            var bytes = Encoding.UTF8.GetBytes(clientLogs);
            if (bytes.Length > _supportEmailOptions.MaxDiagnosticsBytes)
            {
                bytes = bytes.Take((int)_supportEmailOptions.MaxDiagnosticsBytes).ToArray();
            }

            attachments.Add(new EmailAttachment(
                "sugarguard-mobile-logs-last-hour.txt",
                "text/plain; charset=utf-8",
                bytes));
        }

        return attachments;
    }

    private async Task SendSupportEmailAsync(
        SupportConversation conversation,
        SupportMessage message,
        string? requesterEmail,
        IReadOnlyCollection<EmailAttachment> attachments,
        CancellationToken cancellationToken)
    {
        var replyTo = conversation.CallbackEmail ?? requesterEmail;
        var requester = string.IsNullOrWhiteSpace(replyTo) ? "email не указан" : replyTo.Trim();
        var plainText = $"""
            Новое обращение в поддержку SugarGuard

            Номер: {conversation.ConversationId}
            Пользователь: {requester}
            Тема: {conversation.Subject}
            Дата: {message.CreatedAt:O}

            Сообщение:
            {message.Body}

            Ответьте пользователю по email: {requester}
            """;
        var html = $"""
            <h2>Новое обращение в поддержку SugarGuard</h2>
            <p><strong>Номер:</strong> {conversation.ConversationId}</p>
            <p><strong>Пользователь:</strong> {WebUtility.HtmlEncode(requester)}</p>
            <p><strong>Тема:</strong> {WebUtility.HtmlEncode(conversation.Subject)}</p>
            <p><strong>Дата:</strong> {message.CreatedAt:O}</p>
            <h3>Сообщение</h3>
            <pre style="white-space:pre-wrap;font-family:Arial,sans-serif">{WebUtility.HtmlEncode(message.Body)}</pre>
            <p>Ответьте пользователю по email: <a href="mailto:{WebUtility.HtmlEncode(requester)}">{WebUtility.HtmlEncode(requester)}</a></p>
            """;

        await _emailService.SendAsync(
            _supportEmailOptions.InboxEmail,
            $"[SugarGuard Support] {conversation.Subject}",
            html,
            plainText,
            attachments,
            cancellationToken);
    }

    private async Task SendSupportReplyEmailAsync(
        SupportConversation conversation,
        SupportMessage message,
        CancellationToken cancellationToken)
    {
        var recipient = conversation.CallbackEmail ?? conversation.RequesterUser.EmailForLogin;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            _logger.LogWarning(
                "Не удалось отправить ответ поддержки по email: в обращении {ConversationId} не указан адрес получателя.",
                conversation.ConversationId);
            return;
        }

        var plainText = $"""
            Новый ответ поддержки SugarGuard

            Тема обращения: {conversation.Subject}
            Номер обращения: {conversation.ConversationId}

            Ответ:
            {message.Body}

            Это письмо отправлено из центра поддержки SugarGuard.
            """;

        var html = $"""
            <h2>Новый ответ поддержки SugarGuard</h2>
            <p><strong>Тема обращения:</strong> {WebUtility.HtmlEncode(conversation.Subject)}</p>
            <p><strong>Номер обращения:</strong> {conversation.ConversationId}</p>
            <h3>Ответ</h3>
            <pre style="white-space:pre-wrap;font-family:Arial,sans-serif">{WebUtility.HtmlEncode(message.Body)}</pre>
            <p>Это письмо отправлено из центра поддержки SugarGuard.</p>
            """;

        await _emailService.SendAsync(
            recipient.Trim(),
            $"[SugarGuard Support] Ответ: {conversation.Subject}",
            html,
            plainText,
            Array.Empty<EmailAttachment>(),
            cancellationToken);
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
        CallbackEmail = conversation.CallbackEmail,
        CreatedAt = conversation.CreatedAt,
        UpdatedAt = conversation.UpdatedAt,
        LastMessagePreview = conversation.Messages
            .OrderByDescending(message => message.CreatedAt)
            .Select(message => message.Body)
            .FirstOrDefault() ?? string.Empty,
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
    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        try
        {
            return new MailAddress(email.Trim()).Address;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Указан некорректный email для ответа.", nameof(email), exception);
        }
    }
}
