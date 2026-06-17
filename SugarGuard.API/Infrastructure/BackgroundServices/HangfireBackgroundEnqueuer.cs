using Hangfire;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Infrastructure.Jobs;

namespace SugarGuard.API.Infrastructure.BackgroundServices;

/// <summary>
/// Enqueuer на базе Hangfire
/// </summary>
public sealed class HangfireBackgroundEnqueuer : IBackgroundEnqueuer
{
    /// <inheritdoc/>
    public void EnqueueExportJob(Guid exportJobId)
    {
        BackgroundJob.Enqueue<ExportJobProcessor>(
            processor => processor.ExecuteAsync(exportJobId, CancellationToken.None));
    }
}
