using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Security;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация привязки ребенка и родителя
/// </summary>

public class ParentLinkService : IParentLinkService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _audit;
    private readonly IConnectionCodeHasher _codeHasher;
    private readonly ILogger<ParentLinkService> _logger;

    public ParentLinkService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService audit,
        IConnectionCodeHasher codeHasher,
        ILogger<ParentLinkService> logger)
    {
        _dbFactory = dbFactory;
        _audit = audit;
        _codeHasher = codeHasher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> ChildExistsAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Children
            .AsNoTracking()
            .AnyAsync(c => c.ChildId == childId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SaveConnectionCodeResult> SaveConnectionCodeAsync(
        SaveConnectionCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (!await db.Children.AnyAsync(c => c.ChildId == request.ChildId, cancellationToken))
        {
            return new SaveConnectionCodeResult(
                Success: false, CodeId: null, ExpiresAt: null, ErrorMessage: "Child not found");
        }

        // Удаляем все неиспользованные коды для этого ребёнка 
        var oldCodes = await db.ConnectionCodes
            .Where(c => c.ChildId == request.ChildId && !c.IsUsed)
            .ToListAsync(cancellationToken);

        if (oldCodes.Count > 0)
        {
            db.ConnectionCodes.RemoveRange(oldCodes);
        }

        var codeHash = _codeHasher.Hash(request.Code);

        var connectionCode = new ConnectionCode
        {
            ChildId = request.ChildId,
            CodeHash = codeHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false
        };

        db.ConnectionCodes.Add(connectionCode);
        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            "parent_link.code_saved",
            "ConnectionCode",
            connectionCode.CodeId.ToString(),
            $"Child={request.ChildId}",
            CancellationToken.None);

        return new SaveConnectionCodeResult(
            Success: true, CodeId: connectionCode.CodeId, ExpiresAt: connectionCode.ExpiresAt,
            ErrorMessage: null);
    }

    /// <inheritdoc/>
    public async Task<VerifyConnectionCodeResult> VerifyConnectionCodeAsync(
        VerifyConnectionCodeRequest request,
        CancellationToken cancellationToken = default)
        => await VerifyExternalConnectionCodeAsync(
            request.ConnectionCode,
            request.TelegramUserId,
            isMax: false,
            cancellationToken);

    /// <inheritdoc/>
    public async Task<VerifyConnectionCodeResult> VerifyMaxConnectionCodeAsync(
        VerifyMaxConnectionCodeRequest request,
        CancellationToken cancellationToken = default)
        => await VerifyExternalConnectionCodeAsync(
            request.ConnectionCode,
            request.MaxUserId,
            isMax: true,
            cancellationToken);

    private async Task<VerifyConnectionCodeResult> VerifyExternalConnectionCodeAsync(
        string rawConnectionCode,
        long messengerUserId,
        bool isMax,
        CancellationToken cancellationToken)
    {
        var codeHash = _codeHasher.Hash(rawConnectionCode);
        var utcNow = DateTime.UtcNow;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var isPostgres = db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        var sql = isPostgres
            ? @"SELECT * FROM connection_codes
                WHERE code_hash = {0}
                  AND is_used = FALSE
                  AND expires_at > {1}
                FOR UPDATE SKIP LOCKED"
            : @"SELECT * FROM connection_codes
                WHERE code_hash = {0}
                  AND is_used = FALSE
                  AND expires_at > {1}";

        var connectionCode = await db.ConnectionCodes
            .FromSqlRaw(sql, codeHash, utcNow)
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (connectionCode is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return new VerifyConnectionCodeResult(
                Success: true, IsValid: false, ChildId: null, LinkId: null,
                Message: null, ErrorMessage: "Invalid or expired code");
        }

        // Поиск существующей привязки
        var existingLink = await db.ParentChildLinks
            .Include(l => l.ParentUser)
            .FirstOrDefaultAsync(l =>
                (isMax ? l.ParentUser.MaxUserId : l.ParentUser.TelegramId) == messengerUserId &&
                l.ChildId == connectionCode.ChildId,
                cancellationToken);

        if (existingLink is not null)
        {
            // Код использован, но связь уже есть
            connectionCode.IsUsed = true;
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _audit.WriteAsync(
                "parent_link.already_linked",
                "ParentChildLink",
                existingLink.LinkId.ToString(),
                $"Parent={existingLink.ParentUserId};Child={existingLink.ChildId}",
                CancellationToken.None);

            return new VerifyConnectionCodeResult(
                Success: true, IsValid: true, ChildId: connectionCode.ChildId,
                LinkId: existingLink.LinkId,
                Message: "Link already exists", ErrorMessage: null);
        }

        // Создаём User, если его ещё нет
        var user = await db.Users.FirstOrDefaultAsync(
            u => (isMax ? u.MaxUserId : u.TelegramId) == messengerUserId, cancellationToken);

        if (user is null)
        {
            user = isMax
                ? new User { MaxUserId = messengerUserId }
                : new User { TelegramId = messengerUserId };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        var parentChildLink = new ParentChildLink
        {
            ParentUserId = user.UserId,
            ChildId = connectionCode.ChildId
        };

        db.ParentChildLinks.Add(parentChildLink);
        connectionCode.IsUsed = true;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        await _audit.WriteAsync(
            isMax ? "parent_link.max_verified" : "parent_link.verified",
            "ParentChildLink",
            parentChildLink.LinkId.ToString(),
            $"Parent={user.UserId};Child={connectionCode.ChildId};{(isMax ? "MAX" : "Telegram")}={messengerUserId}",
            CancellationToken.None);

        _logger.LogInformation(
            "Parent-child link created. LinkId={LinkId}, Parent={ParentId}, Child={ChildId}.",
            parentChildLink.LinkId, user.UserId, connectionCode.ChildId);

        return new VerifyConnectionCodeResult(
            Success: true, IsValid: true, ChildId: connectionCode.ChildId,
            LinkId: parentChildLink.LinkId,
            Message: "Link created", ErrorMessage: null);
    }
}
