namespace SugarGuard.Web.Services;


public interface ITokenStore
{
    Task<string?> GetTokenAsync();

    Task SetTokenAsync(string token);

    Task RemoveTokenAsync();

    Task<RefreshAccessTokenResult?> RefreshAccessTokenAsync(string accessToken);

    Task SetRefreshTokenAsync(string refreshToken);
    Task RemoveRefreshTokenAsync();
}

/// <summary>
/// Результат обновления access-токена через refresh cookie.
/// </summary>
public sealed record RefreshAccessTokenResult(string AccessToken);
