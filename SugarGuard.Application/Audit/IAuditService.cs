namespace SugarGuard.Application.Audit;

public interface IAuditService
{
    Task WriteAsync(string action, string? targetType = null, string? targetId = null, string? details = null, CancellationToken cancellationToken = default);
}
