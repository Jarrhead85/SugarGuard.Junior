using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace SugarGuard.Web.Services;

/// <summary>
/// Blazor Server — провайдер аутентификации на основе JWT
/// </summary>
public sealed class JwtAuthStateProvider : AuthenticationStateProvider
{
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(5);

    private readonly ITokenStore _tokenStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtAuthStateProvider> _logger;

    /// <summary>
    /// Конструктор — все зависимости через DI
    /// </summary>
    public JwtAuthStateProvider(
        ITokenStore tokenStore,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<JwtAuthStateProvider> logger)
    {
        _tokenStore = tokenStore;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }
   
    public event Action? OnSessionExpired; // Вызывается при получении 401 от API
   
    internal void NotifySessionExpired() => OnSessionExpired?.Invoke(); // Вызывает событие истечения сессии из HTTP-обработчика 401

    // Ядро AuthenticationStateProvider
    /// <inheritdoc/>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            string? token;
            try
            {
                token = await _tokenStore.GetTokenAsync();
            }
            catch (Exception ex) when (ex is InvalidOperationException or JSException)
            {
                return Anonymous;
            }

            // Токена нет — анонимный пользователь
            if (string.IsNullOrWhiteSpace(token))
                return Anonymous;

            // Токен ещё действителен — строим principal сразу
            if (!IsTokenExpired(token))
                return new AuthenticationState(BuildPrincipal(token));

            // Access-токен истёк — пробуем Refresh Token Rotation
            string? refreshToken;
            try
            {
                refreshToken = await _tokenStore.GetRefreshTokenAsync();
            }
            catch (Exception ex) when (ex is InvalidOperationException or JSException)
            {
                return Anonymous;
            }

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                var refreshed = await TryRefreshAsync(token, refreshToken);
                if (refreshed is not null)
                {
                    try
                    {
                        await _tokenStore.SetTokenAsync(refreshed.AccessToken!);
                        await _tokenStore.SetRefreshTokenAsync(refreshed.RefreshToken!);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or JSException)
                    {
                        _logger.LogWarning(ex, "JwtAuthStateProvider: не удалось сохранить обновлённый токен.");
                    }
                    _logger.LogInformation("JwtAuthStateProvider: токен успешно обновлён через refresh.");
                    return new AuthenticationState(BuildPrincipal(refreshed.AccessToken!));
                }
            }

            _logger.LogWarning("JwtAuthStateProvider: refresh не удался, сессия сброшена.");
            try
            {
                await _tokenStore.RemoveTokenAsync();
                await _tokenStore.RemoveRefreshTokenAsync();
            }
            catch (Exception ex) when (ex is InvalidOperationException or JSException)
            {
                _logger.LogWarning(ex, "JwtAuthStateProvider: не удалось очистить токены из хранилища.");
            }
            return Anonymous;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JwtAuthStateProvider: ошибка в GetAuthenticationStateAsync.");
            return Anonymous;
        }
    }

    /// <summary>
    /// Принудительно уведомляет Blazor об изменении состояния аутентификации
    /// </summary>
    public void NotifyAuthStateChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    // Логин / Логаут
    /// <summary>
    /// Выполняет POST api/auth/login
    /// </summary>
    public async Task<string?> LoginAsync(string email, string password)
    {
        var client = _httpClientFactory.CreateClient("SugarGuardApi");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("api/auth/login", new { email, password });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JwtAuthStateProvider: сетевая ошибка при логине.");
            return "Сервер недоступен. Проверьте подключение.";
        }

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var errorBody = await response.Content.ReadFromJsonAsync<LoginErrorResponse>();
                return errorBody?.ErrorMessage ?? errorBody?.Message ?? "Неверный email или пароль.";
            }
            catch
            {
                return $"Ошибка сервера: {(int)response.StatusCode}.";
            }
        }

        LoginSuccessResponse? loginResult;
        try
        {
            loginResult = await response.Content.ReadFromJsonAsync<LoginSuccessResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JwtAuthStateProvider: не удалось десериализовать ответ логина.");
            return "Неожиданный ответ сервера.";
        }

        var accessToken = loginResult?.AccessToken ?? loginResult?.Token;
        if (string.IsNullOrWhiteSpace(accessToken))
            return "Сервер не вернул токен.";

        await _tokenStore.SetTokenAsync(accessToken);

        if (!string.IsNullOrWhiteSpace(loginResult?.RefreshToken))
            await _tokenStore.SetRefreshTokenAsync(loginResult.RefreshToken);

        var principal = BuildPrincipal(accessToken);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));

        _logger.LogInformation("JwtAuthStateProvider: пользователь {Email} вошёл в систему.", email);
        return null;
    }

    /// <summary>
    /// Выполняет логаут
    /// </summary>
    public async Task LogoutAsync()
    {
        var refreshToken = await _tokenStore.GetRefreshTokenAsync();
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("SugarGuardApi");
                var accessToken = await _tokenStore.GetTokenAsync();
                if (!string.IsNullOrWhiteSpace(accessToken))
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", accessToken);

                await client.PostAsJsonAsync("api/auth/logout", new { refreshToken });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JwtAuthStateProvider: не удалось отозвать refresh-токен на сервере.");
            }
        }

        await _tokenStore.RemoveTokenAsync();
        await _tokenStore.RemoveRefreshTokenAsync();

        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
        _logger.LogInformation("JwtAuthStateProvider: пользователь вышел из системы.");
    }

    // Вспомогательные методы
    /// <summary>
    /// Выполняет POST api/auth/refresh
    /// </summary>
    private async Task<LoginSuccessResponse?> TryRefreshAsync(string accessToken, string refreshToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SugarGuardApi");
            var res = await client.PostAsJsonAsync(
                "api/auth/refresh",
                new { accessToken, refreshToken });

            if (!res.IsSuccessStatusCode)
                return null;

            var data = await res.Content.ReadFromJsonAsync<LoginSuccessResponse>();
            return string.IsNullOrWhiteSpace(data?.AccessToken ?? data?.Token) ? null : data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JwtAuthStateProvider: TryRefreshAsync завершился с ошибкой.");
            return null;
        }
    }

    /// <summary>
    /// Строит ClaimsPrincipal из JWT без сетевых вызовов
    /// </summary>
    private static ClaimsPrincipal BuildPrincipal(string token)
    {
        var claims = ParseClaims(token);
        var identity = new ClaimsIdentity(claims, "JwtAuth");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Возвращает true, если JWT истёк с учётом ClockSkewTolerance
    /// </summary>
    private static bool IsTokenExpired(string token)
    {
        try
        {
            var payload = DecodePayload(token);
            if (payload.TryGetProperty("exp", out var expElement))
            {
                var expUtc = DateTimeOffset
                    .FromUnixTimeSeconds(expElement.GetInt64())
                    .UtcDateTime;
                return DateTime.UtcNow > expUtc - ClockSkewTolerance;
            }
            return true;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Разбирает payload JWT в список Claim
    /// </summary>
    private static IEnumerable<Claim> ParseClaims(string token)
    {
        var claims = new List<Claim>();
        JsonElement payload;
        try
        {
            payload = DecodePayload(token);
        }
        catch
        {
            return claims;
        }

        foreach (var property in payload.EnumerateObject())
        {
            var claimType = property.Name switch
            {
                "sub" => ClaimTypes.NameIdentifier,
                "email" => ClaimTypes.Email,
                "name" => ClaimTypes.Name,
                  "role" => ClaimTypes.Role,
                "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" => ClaimTypes.Role,
                "UserId" => "UserId",
                _ => property.Name
            };

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in property.Value.EnumerateArray())
                    claims.Add(new Claim(claimType, element.GetString() ?? string.Empty));
            }
            else
            {
                claims.Add(new Claim(claimType, property.Value.ToString()));
            }
        }

        return claims;
    }

    /// <summary>
    /// Декодирует base64url-закодированный payload JWT без проверки подписи
    /// </summary>
    private static JsonElement DecodePayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            throw new FormatException("JWT должен содержать ровно 3 части.");

        var payloadBase64 = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');

        payloadBase64 += (payloadBase64.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };

        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64));
        return JsonDocument.Parse(payloadJson).RootElement;
    }

    // Константы
    /// <summary>
    /// Анонимный (неаутентифицированный) AuthenticationState
    /// </summary>
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));


    // Внутренние DTO
    /// <summary>
    /// DTO ответа POST api/auth/login и api/auth/refresh
    /// </summary>
    private sealed class LoginSuccessResponse
    {       
        public string? AccessToken { get; set; } // Access-токен       
        public string? Token { get; set; } // Access-токен       
        public string? RefreshToken { get; set; } // Refresh-токен
    }

    /// <summary>
    /// DTO ошибки POST api/auth/login
    /// </summary>
    private sealed class LoginErrorResponse
    {       
        public string? ErrorMessage { get; set; } // Сообщение об ошибке от API       
        public string? Message { get; set; } // Legacy-поле сообщения об ошибке
    }
}
