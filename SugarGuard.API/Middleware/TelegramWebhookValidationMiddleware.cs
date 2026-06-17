using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SugarGuard.API.Configuration;

namespace SugarGuard.API.Middleware;

/// <summary>
/// Middleware проверки подлинности входящих Telegram Webhook-запросов
/// </summary>

public sealed class TelegramWebhookValidationMiddleware
{
   
    private const string WebhookSecretHeader = "X-Telegram-Bot-Api-Secret-Token"; // Заголовок, который Telegram добавляет к каждому webhook-запросу
       
    private const string WebhookPath = "/api/telegram/webhook"; // Путь, к которому применяется проверка

    private readonly RequestDelegate _next;
    private readonly ILogger<TelegramWebhookValidationMiddleware> _logger;

    private readonly byte[]? _secretBytes;
    private readonly bool _isConfigured;

    /// <summary>
    /// Инициализирует middleware и кэширует секрет в байтовом представлении
    /// </summary>
    public TelegramWebhookValidationMiddleware(
        RequestDelegate next,
        IOptions<BotSettings> botSettings,
        ILogger<TelegramWebhookValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        var secret = botSettings.Value.WebhookSecret;

        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning(
                "TelegramWebhookValidation: WebhookSecret не задан в конфигурации. " +
                "Все запросы к {Path} будут отклоняться.", WebhookPath);

            _isConfigured = false;
            _secretBytes = null;
        }
        else
        {
            _isConfigured = true;
            _secretBytes = Encoding.UTF8.GetBytes(secret);
        }
    }

    /// <summary>
    /// Обрабатывает входящий HTTP-запрос
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(
                WebhookPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!_isConfigured || _secretBytes is null)
        {
            _logger.LogError(
                "TelegramWebhookValidation: запрос к {Path} отклонён — WebhookSecret не сконфигурирован.",
                WebhookPath);

            await WriteJsonForbiddenAsync(context, "webhook_not_configured",
                "Webhook endpoint не сконфигурирован.");
            return;
        }

        // Читаем заголовок из запроса
        var headerValue = context.Request.Headers[WebhookSecretHeader].FirstOrDefault();

        if (string.IsNullOrEmpty(headerValue))
        {
            _logger.LogWarning(
                "TelegramWebhookValidation: отсутствует заголовок {Header}. IP={IP}.",
                WebhookSecretHeader,
                context.Connection.RemoteIpAddress);

            await WriteJsonForbiddenAsync(context, "missing_secret_token",
                "Отсутствует заголовок X-Telegram-Bot-Api-Secret-Token.");
            return;
        }

        var incomingBytes = Encoding.UTF8.GetBytes(headerValue);

        var isValid = _secretBytes.Length == incomingBytes.Length
                      && CryptographicOperations.FixedTimeEquals(_secretBytes, incomingBytes);

        if (!isValid)
        {
            _logger.LogWarning(
                "TelegramWebhookValidation: неверный секрет. IP={IP} UserAgent={UA}.",
                context.Connection.RemoteIpAddress,
                context.Request.Headers.UserAgent.ToString());

            await WriteJsonForbiddenAsync(context, "invalid_secret_token",
                "Недействительный секретный токен.");
            return;
        }

        _logger.LogDebug(
            "TelegramWebhookValidation: токен подтверждён. IP={IP}.",
            context.Connection.RemoteIpAddress);

        await _next(context);
    }

    /// <summary>
    /// Записывает JSON-ответ с кодом 403 Forbidden
    /// </summary>
    private static async Task WriteJsonForbiddenAsync(
        HttpContext context,
        string errorCode,
        string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(new
        {
            error = errorCode,
            message
        });
    }
}
