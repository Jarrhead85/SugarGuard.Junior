namespace SugarGuard.Web.Pages.Doctor;

public partial class Profile : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        _profileRefreshCts.Cancel();

        if (_profileRefreshTask is not null)
        {
            try
            {
                await _profileRefreshTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _profileRefreshCts.Dispose();
    }
}
