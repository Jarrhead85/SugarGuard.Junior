using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SugarGuard.Web.Endpoints;

/// <summary>
/// Конечные точки, которые изолируют refresh-токен в httpOnly cookie портала.
/// </summary>
public static class SessionCookieEndpointExtensions
{
    private const string RefreshCookieName = "sg_refresh";

    public static void MapSessionCookieEndpoints(this WebApplication app)
    {
        app.MapPost("/session/refresh-token", (
            [Microsoft.AspNetCore.Mvc.FromBody] RefreshTokenCookieRequest request,
            HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 1024)
            {
                return Results.BadRequest();
            }

            WriteRefreshCookie(context, request.RefreshToken);
            return Results.NoContent();
        }).AllowAnonymous();

        app.MapPost("/session/refresh", async (
            [Microsoft.AspNetCore.Mvc.FromBody] RefreshAccessTokenRequest request,
            HttpContext context,
            [Microsoft.AspNetCore.Mvc.FromServices] IHttpClientFactory httpClientFactory,
            CancellationToken cancellationToken) =>
        {
            var refreshToken = context.Request.Cookies[RefreshCookieName];
            if (string.IsNullOrWhiteSpace(refreshToken)
                || string.IsNullOrWhiteSpace(request.AccessToken)
                || request.AccessToken.Length > 8192)
            {
                DeleteRefreshCookie(context);
                return Results.Unauthorized();
            }

            var client = httpClientFactory.CreateClient("SugarGuardApiPublic");
            using var response = await client.PostAsJsonAsync(
                "api/auth/refresh",
                new { accessToken = request.AccessToken, refreshToken },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                DeleteRefreshCookie(context);
                return Results.Unauthorized();
            }

            var refreshed = await response.Content.ReadFromJsonAsync<ApiRefreshResponse>(cancellationToken);
            if (string.IsNullOrWhiteSpace(refreshed?.AccessToken)
                || string.IsNullOrWhiteSpace(refreshed.RefreshToken))
            {
                DeleteRefreshCookie(context);
                return Results.Unauthorized();
            }

            WriteRefreshCookie(context, refreshed.RefreshToken);
            return Results.Ok(new RefreshAccessTokenResponse(refreshed.AccessToken));
        }).AllowAnonymous();

        app.MapDelete("/session/refresh-token", async (
            [Microsoft.AspNetCore.Mvc.FromBody] SessionLogoutRequest request,
            HttpContext context,
            [Microsoft.AspNetCore.Mvc.FromServices] IHttpClientFactory httpClientFactory,
            CancellationToken cancellationToken) =>
        {
            var refreshToken = context.Request.Cookies[RefreshCookieName];
            DeleteRefreshCookie(context);

            if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(request.AccessToken))
            {
                return Results.NoContent();
            }

            var client = httpClientFactory.CreateClient("SugarGuardApiPublic");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);
            using var response = await client.PostAsJsonAsync(
                "api/auth/logout",
                new { refreshToken },
                cancellationToken);

            return response.IsSuccessStatusCode ? Results.NoContent() : Results.StatusCode(StatusCodes.Status502BadGateway);
        }).AllowAnonymous();
    }

    private static void WriteRefreshCookie(HttpContext context, string refreshToken)
    {
        context.Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = "/",
            MaxAge = TimeSpan.FromDays(30)
        });
    }

    private static void DeleteRefreshCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }

    private sealed record RefreshTokenCookieRequest(string? RefreshToken);
    private sealed record RefreshAccessTokenRequest(string? AccessToken);
    private sealed record SessionLogoutRequest(string? AccessToken);
    private sealed record RefreshAccessTokenResponse(string AccessToken);
    private sealed record ApiRefreshResponse(string? AccessToken, string? RefreshToken);
}
