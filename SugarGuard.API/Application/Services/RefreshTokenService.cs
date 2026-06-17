using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация сервиса токенов
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int TokenByteLength = 32;
    private const int DefaultExpiryDays = 30;

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RefreshTokenService> _logger;
    private readonly IAuditService _audit;
    private readonly IWebPushService _webPush;
    private readonly IEmailService _email;

    /// <summary>
    /// Инициализирует сервис
    /// </summary>
    public RefreshTokenService(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<RefreshTokenService> logger,
        IAuditService audit,
        IWebPushService webPush,
        IEmailService email)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _audit = audit;
        _webPush = webPush;
        _email = email;
    }

    // Создание
    /// <inheritdoc/>
    public async Task<(string PlainToken, RefreshToken Entity)> CreateAsync(
        string userId,
        string? createdByIp,
        string? createdByUserAgent,
        CancellationToken cancellationToken = default)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        var plainToken = Convert.ToBase64String(randomBytes);
        var tokenHash = ComputeSha256(plainToken);

        var expiryDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpiryDays", DefaultExpiryDays);

        var entity = new RefreshToken
        {
            Token = tokenHash,
            UserId = Guid.Parse(userId),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            IsRevoked = false,
            CreatedByIp = createdByIp,
            CreatedByUserAgent = createdByUserAgent?[..Math.Min(createdByUserAgent.Length, 256)]
        };

        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Создан refresh-токен. UserId={UserId} ExpiresAt={ExpiresAt}",
            userId, entity.ExpiresAt);

        return (plainToken, entity);
    }

    // Валидация
    /// <inheritdoc/>
    public async Task<RefreshToken?> ValidateAsync(
        string plainToken,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeSha256(plainToken);
        var userGuid = Guid.Parse(userId);
        var entity = await _context.RefreshTokens
            .FirstOrDefaultAsync(
                t => t.Token == tokenHash && t.UserId == userGuid,
                cancellationToken);

        if (entity is null)
        {
            _logger.LogWarning(
                "Refresh-токен не найден в БД. UserId={UserId}", userId);
            return null;
        }

        if (entity.ReplacedByToken is not null)
        {
            _logger.LogError(
                "Повторное использование ротированного токена! UserId={UserId}. Отзываем все токены.",
                userId);
            await NotifyTokenReuseAsync(userId, cancellationToken);

            await RevokeAllForUserAsync(userId, "reuse_detected", cancellationToken);
            return null;
        }

        if (entity.IsRevoked)
        {
            _logger.LogWarning(
                "Refresh-токен уже отозван. UserId={UserId} Reason={Reason}",
                userId, entity.RevokedReason);
            return null;
        }

        if (entity.IsExpired)
        {
            _logger.LogWarning(
                "Refresh-токен истёк. UserId={UserId} ExpiresAt={ExpiresAt}",
                userId, entity.ExpiresAt);
            return null;
        }

        return entity;
    }

    // Ротация
    /// <inheritdoc/>
    public async Task<string> RotateAsync(
        RefreshToken existingToken,
        string userId,
        string? createdByIp,
        string? createdByUserAgent,
        CancellationToken cancellationToken = default)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        var newPlainToken = Convert.ToBase64String(randomBytes);
        var newTokenHash = ComputeSha256(newPlainToken);

        var expiryDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpiryDays", DefaultExpiryDays);

        var newEntity = new RefreshToken
        {
            Token = newTokenHash,
            UserId = Guid.Parse(userId),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            IsRevoked = false,
            CreatedByIp = createdByIp,
            CreatedByUserAgent = createdByUserAgent?[..Math.Min(createdByUserAgent?.Length ?? 0, 256)]
        };

        existingToken.IsRevoked = true;
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.RevokedReason = "rotation";
        existingToken.ReplacedByToken = newTokenHash;

        _context.RefreshTokens.Add(newEntity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Ротация refresh-токена. UserId={UserId} ExpiresAt={ExpiresAt}",
            userId, newEntity.ExpiresAt);

        return newPlainToken;
    }

    // Отзыв
    /// <inheritdoc/>
    public async Task RevokeAsync(
        string plainToken,
        string userId,
        string reason = "logout",
        CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeSha256(plainToken);
        var userGuid = Guid.Parse(userId);
        var entity = await _context.RefreshTokens
            .FirstOrDefaultAsync(
                t => t.Token == tokenHash && t.UserId == userGuid,
                cancellationToken);

        if (entity is null || entity.IsRevoked)
            return;

        entity.IsRevoked = true;
        entity.RevokedAt = DateTime.UtcNow;
        entity.RevokedReason = reason;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Refresh-токен отозван. UserId={UserId} Reason={Reason}",
            userId, reason);
    }

    /// <inheritdoc/>
    public async Task RevokeAllForUserAsync(
        string userId,
        string reason = "revoke_all",
        CancellationToken cancellationToken = default)
    {
        var userGuid = Guid.Parse(userId);
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userGuid && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
            token.RevokedReason = reason;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Отозваны все refresh-токены. UserId={UserId} Count={Count} Reason={Reason}",
            userId, activeTokens.Count, reason);
    }

    // Очистка устаревших токенов 
    /// <inheritdoc/>
    public async Task<int> PurgeExpiredAsync(
        DateTime? olderThan = null,
        CancellationToken cancellationToken = default)
    {
        var cutoff = olderThan ?? DateTime.UtcNow.AddDays(-30);

        var toDelete = await _context.RefreshTokens
            .Where(t =>
                (t.IsRevoked && t.RevokedAt != null && t.RevokedAt < cutoff) ||
                (!t.IsRevoked && t.ExpiresAt < cutoff))
            .ToListAsync(cancellationToken);

        if (toDelete.Count == 0)
            return 0;

        _context.RefreshTokens.RemoveRange(toDelete);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Очищено устаревших refresh-токенов. Count={Count} OlderThan={Cutoff}",
            toDelete.Count, cutoff);

        return toDelete.Count;
    }

    /// <summary>
    /// Вычисляет хэш строки в hex-формате
    /// </summary>
    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Отправляет три независимых сигнала об инциденте повторного использования токена
    /// </summary>
    private async Task NotifyTokenReuseAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _audit.WriteAsync(
                action: "auth.refresh.reuse_detected",
                targetType: "User",
                targetId: userId,
                details: $"UserId={userId};Reason=reuse_detected",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotifyTokenReuse: не удалось записать audit-событие. UserId={UserId}.",
                userId);
        }

        try
        {
            await _webPush.SendNotificationAsync(
                userId: Guid.Parse(userId),
                title: "Внимание: вход в аккаунт",
                body: "Обнаружено повторное использование токена. Все сессии были отозваны. Если это были не вы — смените пароль немедленно.",
                url: "/security/recent-logins",
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "NotifyTokenReuse: не удалось отправить push-уведомление. UserId={UserId}.",
                userId);
        }

        try
        {
            var email = await _context.Users
                .Where(u => u.UserId == Guid.Parse(userId))
                .Select(u => u.EmailForLogin)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(email))
            {
                const string subject = "[SugarGuard] Подозрительная активность в аккаунте";
                const string htmlBody = """
                    <h2>Внимание: подозрительная активность</h2>
                    <p>Мы обнаружили попытку повторного использования вашего refresh-токена.
                       Это типичный признак того, что злоумышленник мог получить доступ к вашему аккаунту.</p>
                    <p><strong>Что мы сделали:</strong> все активные сессии отозваны, для входа потребуется заново ввести логин и пароль.</p>
                    <p><strong>Если это были не вы:</strong> немедленно смените пароль и проверьте устройства, на которых вы авторизованы.</p>
                    <p>Команда SugarGuard</p>
                    """;

                await _email.SendAsync(email, subject, htmlBody, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "NotifyTokenReuse: email пользователя не найден. UserId={UserId}.",
                    userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotifyTokenReuse: не удалось отправить email. UserId={UserId}.",
                userId);
        }
    }
}
