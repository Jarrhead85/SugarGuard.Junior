namespace SugarGuard.Junior.Services.Interfaces;

public interface IAppUpdateService
{
    Task CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
