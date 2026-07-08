namespace SugarGuard.Web.Pages.Doctor;

public partial class NotesIndex : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_refreshTask is not null)
        {
            try
            {
                await _refreshTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
    }
}
