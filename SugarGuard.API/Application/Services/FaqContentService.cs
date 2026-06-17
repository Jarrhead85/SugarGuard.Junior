using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация FAQ-статей
/// </summary>
public class FaqContentService : IFaqContentService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _audit;

    public FaqContentService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService audit)
    {
        _dbFactory = dbFactory;
        _audit = audit;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FaqArticleResponse>> GetPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.FaqArticles
            .AsNoTracking()
            .Where(i => i.IsPublished)
            .OrderByDescending(i => i.UpdatedAt)
            .Select(i => new FaqArticleResponse
            {
                FaqArticleId = i.FaqArticleId,
                Title = i.Title,
                Content = i.Content,
                IsPublished = i.IsPublished,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FaqArticleResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.FaqArticles
            .AsNoTracking()
            .OrderByDescending(i => i.UpdatedAt)
            .Select(i => new FaqArticleResponse
            {
                FaqArticleId = i.FaqArticleId,
                Title = i.Title,
                Content = i.Content,
                IsPublished = i.IsPublished,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<FaqArticleResponse> CreateAsync(
        FaqArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = new FaqArticle
        {
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            IsPublished = request.IsPublished,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.FaqArticles.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
        }

        await _audit.WriteAsync(
            action: "faq.created",
            targetType: "FaqArticle",
            targetId: entity.FaqArticleId.ToString(),
            details: entity.Title,
            cancellationToken: CancellationToken.None);

        return Map(entity);
    }

    /// <inheritdoc/>
    public async Task<FaqArticleResponse?> UpdateAsync(
        Guid id,
        FaqArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.FaqArticles
            .FirstOrDefaultAsync(i => i.FaqArticleId == id, cancellationToken);

        if (entity is null)
            return null;

        entity.Title = request.Title.Trim();
        entity.Content = request.Content.Trim();
        entity.IsPublished = request.IsPublished;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: "faq.updated",
            targetType: "FaqArticle",
            targetId: entity.FaqArticleId.ToString(),
            details: entity.Title,
            cancellationToken: CancellationToken.None);

        return Map(entity);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string? title = null;

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var entity = await db.FaqArticles
                .FirstOrDefaultAsync(i => i.FaqArticleId == id, cancellationToken);

            if (entity is null)
                return false;

            title = entity.Title;
            db.FaqArticles.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }

        await _audit.WriteAsync(
            action: "faq.deleted",
            targetType: "FaqArticle",
            targetId: id.ToString(),
            details: title,
            cancellationToken: CancellationToken.None);

        return true;
    }

    private static FaqArticleResponse Map(FaqArticle entity) => new()
    {
        FaqArticleId = entity.FaqArticleId,
        Title = entity.Title,
        Content = entity.Content,
        IsPublished = entity.IsPublished,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
