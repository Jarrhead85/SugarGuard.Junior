using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

public interface IServerMetricsService
{
    Task<ServerMetricsResponse> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
