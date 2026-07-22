using System.Collections.Concurrent;

namespace SugarGuard.Web.Services;

/// <summary>
/// Кратковременно хранит подготовленные отчёты до скачивания браузером.
/// Идентификатор является одноразовым и действует ограниченное время.
/// </summary>
public sealed class ExportDownloadStore
{
    private const int MaximumPendingExports = 20;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<Guid, PendingExport> _pendingExports = new();
    private readonly TimeProvider _timeProvider;

    public ExportDownloadStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Guid Create(byte[] content, string contentType, string fileName)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        RemoveExpired();

        while (_pendingExports.Count >= MaximumPendingExports)
        {
            var oldest = _pendingExports
                .OrderBy(pair => pair.Value.CreatedAt)
                .FirstOrDefault();

            if (oldest.Key == Guid.Empty)
            {
                break;
            }

            _pendingExports.TryRemove(oldest.Key, out _);
        }

        var ticket = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow();
        _pendingExports[ticket] = new PendingExport(content, contentType, fileName, now, now.Add(Lifetime));
        return ticket;
    }

    public bool TryTake(Guid ticket, out PendingExport export)
    {
        if (!_pendingExports.TryRemove(ticket, out export!))
        {
            return false;
        }

        if (export.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            export = default!;
            return false;
        }

        return true;
    }

    private void RemoveExpired()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var pair in _pendingExports.Where(pair => pair.Value.ExpiresAt <= now))
        {
            _pendingExports.TryRemove(pair.Key, out _);
        }
    }
}

public sealed record PendingExport(
    byte[] Content,
    string ContentType,
    string FileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
