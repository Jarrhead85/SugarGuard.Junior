using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация экспорта CSV
/// </summary>
public class ExportJobApiService : IExportJobApiService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _audit;
    private readonly IBackgroundEnqueuer _enqueuer;
    private readonly ILogger<ExportJobApiService> _logger;

    public ExportJobApiService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService audit,
        IBackgroundEnqueuer enqueuer,
        ILogger<ExportJobApiService> logger)
    {
        _dbFactory = dbFactory;
        _audit = audit;
        _enqueuer = enqueuer;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExportJob> CreateAsync(
        CreateExportJobRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var job = new ExportJob
        {
            RequestedByUserId = requestedByUserId,
            ChildId = request.ChildId,
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            Format = request.Format,
            Status = "queued",
            CreatedAt = DateTime.UtcNow
        };

        // Создаём запись
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.ExportJobs.Add(job);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Ставим в фоновую очередь
        try
        {
            _enqueuer.EnqueueExportJob(job.ExportJobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CreateAsync: ошибка постановки в очередь. ExportJobId={ExportJobId}.",
                job.ExportJobId);

            await using var db = await _dbFactory.CreateDbContextAsync(CancellationToken.None);
            var tracked = await db.ExportJobs
                .FirstOrDefaultAsync(j => j.ExportJobId == job.ExportJobId, CancellationToken.None);

            if (tracked is not null)
            {
                tracked.Status = "failed";
                tracked.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
            }

            throw;
        }

        await _audit.WriteAsync(
            action: "export.created",
            targetType: "ExportJob",
            targetId: job.ExportJobId.ToString(),
            details: $"Child={request.ChildId};Format={request.Format}",
            cancellationToken: CancellationToken.None);

        _logger.LogInformation(
            "CreateAsync: экспорт поставлен в очередь. ExportJobId={ExportJobId} ChildId={ChildId}.",
            job.ExportJobId, request.ChildId);

        return job;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExportJobResponse>> GetListAsync(
        Guid? childId,
        Guid requestedByUserId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.ExportJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .AsQueryable();

        if (childId.HasValue)
        {
            query = query.Where(j => j.ChildId == childId.Value);
        }
        else
        {
            query = query.Where(j => j.RequestedByUserId == requestedByUserId);
        }

        return await query
            .Take(safeLimit)
            .Select(j => new ExportJobResponse
            {
                ExportJobId = j.ExportJobId,
                RequestedByUserId = j.RequestedByUserId,
                ChildId = j.ChildId,
                PeriodFrom = j.PeriodFrom,
                PeriodTo = j.PeriodTo,
                Format = j.Format,
                Status = j.Status,
                DownloadUrl = j.DownloadUrl,
                CreatedAt = j.CreatedAt,
                CompletedAt = j.CompletedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ExportJob?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.ExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.ExportJobId == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RecordDownloadedAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        await _audit.WriteAsync(
            action: "export.downloaded",
            targetType: "ExportJob",
            targetId: jobId.ToString(),
            details: null,
            cancellationToken: CancellationToken.None);
    }
}
