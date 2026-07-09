using SugarGuard.API.Application.Interfaces;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Потокобезопасный кэш access-токена GigaChat
/// </summary>
public sealed class GigaChatTokenCache : IGigaChatTokenCache
{
    /// <summary>
    /// TTL валидного токена
    /// </summary>
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(25);

    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromMinutes(1);

    private string? _token;
    private DateTime _expiry;

    // Singleton lifetime is intentional: the semaphore serializes refreshes and
    // prevents concurrent callers from requesting several GigaChat tokens at once.
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool TryGet(out string? token)
    {
        var isFresh = DateTime.UtcNow < _expiry;
        token = isFresh ? _token : null;
        return isFresh;
    }

    public async Task<string?> GetOrRefreshAsync(Func<Task<string?>> factory)
    {
        if (TryGet(out var cached)) return cached;
        await _lock.WaitAsync();
        try
        {
            if (TryGet(out cached)) return cached;

            _token = await factory();

            if (_token is not null)
            {
                _expiry = DateTime.UtcNow + TokenTtl;
            }
            else
            {
                _expiry = DateTime.UtcNow + NegativeCacheTtl;
            }

            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }
}
