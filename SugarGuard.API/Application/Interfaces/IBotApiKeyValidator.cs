namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Валидатор service-to-service API-ключа для Telegram-бота
/// </summary>
public interface IBotApiKeyValidator
{
    Task<bool> ValidateAsync(string providedKey, CancellationToken cancellationToken = default);
}

