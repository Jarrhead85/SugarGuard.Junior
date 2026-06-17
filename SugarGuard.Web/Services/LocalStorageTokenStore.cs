using Microsoft.JSInterop;

namespace SugarGuard.Web.Services;

/// <summary>
/// Хранит JWT access- и refresh-токены в браузерном localStorage через JS Interop
/// </summary>
public sealed class LocalStorageTokenStore : ITokenStore
{
    private readonly IJSRuntime _js;
    private readonly ILogger<LocalStorageTokenStore> _logger;

    public LocalStorageTokenStore(IJSRuntime js, ILogger<LocalStorageTokenStore> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _js.InvokeAsync<string?>("tokenStore.getToken", TimeSpan.FromSeconds(3));
        }
        catch (InvalidOperationException) { return null; }
        catch (JSDisconnectedException) { return null; }
        catch (JSException) { return null; }
        catch (TaskCanceledException) { return null; }
    }

    /// <inheritdoc/>
    public async Task SetTokenAsync(string token)
    {
        try
        {
            await _js.InvokeVoidAsync("tokenStore.setToken", TimeSpan.FromSeconds(3), token);
        }
        catch (InvalidOperationException) { }
        catch (JSDisconnectedException) { }
        catch (JSException ex) { _logger.LogWarning(ex, "LocalStorageTokenStore: не удалось сохранить access-токен."); }
        catch (TaskCanceledException) { }
    }

    /// <inheritdoc/>
    public async Task RemoveTokenAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("tokenStore.removeToken", TimeSpan.FromSeconds(3));
        }
        catch (InvalidOperationException) { }
        catch (JSDisconnectedException) { }
        catch (JSException ex) { _logger.LogWarning(ex, "LocalStorageTokenStore: не удалось удалить access-токен."); }
        catch (TaskCanceledException) { }
    }

    /// <inheritdoc/>
    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await _js.InvokeAsync<string?>("tokenStore.getRefreshToken", TimeSpan.FromSeconds(3));
        }
        catch (InvalidOperationException) { return null; }
        catch (JSDisconnectedException) { return null; }
        catch (JSException) { return null; }
        catch (TaskCanceledException) { return null; }
    }

    /// <inheritdoc/>
    public async Task SetRefreshTokenAsync(string token)
    {
        try
        {
            await _js.InvokeVoidAsync("tokenStore.setRefreshToken", TimeSpan.FromSeconds(3), token);
        }
        catch (InvalidOperationException) { }
        catch (JSDisconnectedException) { }
        catch (JSException ex) { _logger.LogWarning(ex, "LocalStorageTokenStore: не удалось сохранить refresh-токен."); }
        catch (TaskCanceledException) { }
    }

    /// <inheritdoc/>
    public async Task RemoveRefreshTokenAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("tokenStore.removeRefreshToken", TimeSpan.FromSeconds(3));
        }
        catch (InvalidOperationException) { }
        catch (JSDisconnectedException) { }
        catch (JSException ex) { _logger.LogWarning(ex, "LocalStorageTokenStore: не удалось удалить refresh-токен."); }
        catch (TaskCanceledException) { }
    }
}
