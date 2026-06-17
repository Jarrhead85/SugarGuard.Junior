namespace SugarGuard.API.Configuration;

/// <summary>
/// Настройки Telegram-бота
/// </summary>
public sealed class BotSettings
{   
    public string BotToken { get; init; } = string.Empty; // Токен бота

    public string WebhookUrl { get; init; } = string.Empty; //URL для регистрации webhook в Telegram   

    public string WebhookSecret { get; init; } = string.Empty; // Секрет для проверки подлинности входящих webhook-запросов
   
    public string ApiKey { get; init; } = string.Empty; // API-ключ для Bot Service аутентификации
}
