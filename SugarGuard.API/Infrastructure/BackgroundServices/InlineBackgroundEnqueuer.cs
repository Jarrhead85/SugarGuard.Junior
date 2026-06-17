using Microsoft.Extensions.DependencyInjection;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Infrastructure.Jobs;

namespace SugarGuard.API.Infrastructure.BackgroundServices;

/// <summary>
/// Inline-enqueuer для SQLite-режима
/// </summary>
public sealed class InlineBackgroundEnqueuer : IBackgroundEnqueuer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InlineBackgroundEnqueuer> _logger;

    public InlineBackgroundEnqueuer(
        IServiceScopeFactory scopeFactory,
        ILogger<InlineBackgroundEnqueuer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void EnqueueExportJob(Guid exportJobId)
    {
        // Fire-and-forget. Экспорт продолжит выполняться на ThreadPool
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ExportJobProcessor>();
                await processor.ExecuteAsync(exportJobId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "InlineBackgroundEnqueuer: сбой фоновой обработки экспорта. ExportJobId={ExportJobId}.",
                    exportJobId);
            }
        });
    }
}
