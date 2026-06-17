using System.Security.Claims;
using SugarGuard.Application.Audit;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Сервис аудита
/// </summary>
public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditDetailsRedactor _redactor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IAuditDetailsRedactor redactor,
        ILogger<AuditService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _redactor = redactor;
        _logger = logger;
    }

    public async Task WriteAsync(
        string action,
        string? targetType = null,
        string? targetId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        Guid? actorUserId = null;
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("UserId");

        if (Guid.TryParse(userIdClaim, out var parsedId))
        {
            actorUserId = parsedId;
        }

        // редактируем PHI перед записью в БД
        var redactedDetails = _redactor.Redact(details);

        var log = new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Details = redactedDetails,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);

        try
        {
            await _context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Audit event FAILED to save: Action={Action} TargetType={TargetType} TargetId={TargetId} Actor={Actor}",
                action, targetType, targetId, actorUserId);
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Audit event saved AFTER request cancellation: Action={Action} TargetType={TargetType} TargetId={TargetId} Actor={Actor}",
                action, targetType, targetId, actorUserId);
        }
        else
        {
            _logger.LogInformation(
                "Audit event saved: {Action} {TargetType} {TargetId}",
                action, targetType, targetId);
        }
    }
}
