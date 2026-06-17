namespace SugarGuard.Web.Services;


public interface ITokenStore
{
    Task<string?> GetTokenAsync();

    Task SetTokenAsync(string token);

    Task RemoveTokenAsync();

    Task<string?> GetRefreshTokenAsync();
    Task SetRefreshTokenAsync(string refreshToken);
    Task RemoveRefreshTokenAsync();
}
