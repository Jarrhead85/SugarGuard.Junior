using SugarGuard.Web.Services;

namespace SugarGuard.Tests.Web;

public sealed class ExportDownloadStoreTests
{
    [Fact]
    public void Create_MakesFileAvailableExactlyOnce()
    {
        var store = new ExportDownloadStore(TimeProvider.System);
        var content = new byte[] { 1, 2, 3 };

        var ticket = store.Create(content, "application/pdf", "report.pdf");

        Assert.True(store.TryTake(ticket, out var export));
        Assert.Equal(content, export.Content);
        Assert.Equal("application/pdf", export.ContentType);
        Assert.Equal("report.pdf", export.FileName);
        Assert.False(store.TryTake(ticket, out _));
    }
}
