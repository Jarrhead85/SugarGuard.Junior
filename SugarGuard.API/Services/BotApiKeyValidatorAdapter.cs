using Microsoft.Extensions.Logging;
using SugarGuard.API.Application.Interfaces;

namespace SugarGuard.API.Services;

/// <summary>
/// Адаптер 
/// </summary>
public sealed class BotApiKeyValidatorAdapter : IBotApiKeyValidator
{
    private readonly IAuthService _authService;
    private readonly ILogger<BotApiKeyValidatorAdapter> _logger;

    public BotApiKeyValidatorAdapter(IAuthService authService, ILogger<BotApiKeyValidatorAdapter> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    public Task<bool> ValidateAsync(string providedKey, CancellationToken cancellationToken = default)
    {
        var result = _authService.ValidateBotApiKey(providedKey);

        if (result is null)
        {
            _logger.LogError("BOT_SERVICE_AUTH_KEY is not configured. Service-to-service auth unavailable.");
            throw new InvalidOperationException(
                "Bot service auth key is not configured. Set BOT_SERVICE_AUTH_KEY env var.");
        }

        return Task.FromResult(result.Value);
    }
}

