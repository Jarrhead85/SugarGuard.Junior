namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Кэш access-токена GigaChat для переиспользования между запросами
/// </summary>
public interface IGigaChatTokenCache
{
    bool TryGet(out string? token);

    Task<string?> GetOrRefreshAsync(Func<Task<string?>> factory);
}
