namespace SugarGuard.Junior.Infrastructure.Handlers;

using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Security;

/// <summary>
/// DelegatingHandler, добавляющий Bearer-токен к исходящим HTTP-запросам.
/// Заменяет прямое управление DefaultRequestHeaders в RealApiClient.
/// </summary>
public class JwtAuthorizationHandler : DelegatingHandler
{
    private readonly ISecureStorageService _secureStorage;
    private readonly ILogger<JwtAuthorizationHandler> _logger;

    public JwtAuthorizationHandler(
        ISecureStorageService secureStorage,
        ILogger<JwtAuthorizationHandler> logger)
    {
        _secureStorage = secureStorage;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _secureStorage.GetAccessTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
