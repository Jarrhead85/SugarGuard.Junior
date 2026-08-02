using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace SugarGuard.Bot.Services;

/// <summary>
/// Сервис для управления контекстом пользователя Telegram-бота
/// Использует API для получения и сохранения данных
/// </summary>
public class BotUserContextService : IBotUserContextService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BotUserContextService> _logger;
    private readonly string _baseUrl;
    private readonly string? _botApiKey;

    public BotUserContextService(
        HttpClient httpClient,
        ILogger<BotUserContextService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["BotSettings:ApiUrl"] ?? "https://localhost:7001";

        _botApiKey = Environment.GetEnvironmentVariable("BOT_SERVICE_AUTH_KEY")
                     ?? configuration["BotAuth:ApiKey"]
                     ?? configuration["BotSettings:ApiKey"];

        if (string.IsNullOrEmpty(_botApiKey))
        {
            _logger.LogWarning(
                "BOT_SERVICE_AUTH_KEY is not set — /api/bot-service/context requests will fail with 503.");
        }

        // Настраиваем HttpClient
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SugarGuard-Bot/1.0");

        if (!string.IsNullOrEmpty(_botApiKey))
        {
            const string BotAuthHeader = "X-Bot-Auth";
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(BotAuthHeader, _botApiKey);
        }
    }

    /// <inheritdoc />
    public async Task<Guid?> GetCurrentChildIdAsync(long telegramUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Получение текущего ChildId для Telegram пользователя {TelegramUserId}", telegramUserId);

            var response = await _httpClient.GetAsync($"/api/bot-service/context/{telegramUserId}", cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<BotUserContextResponse>(responseJson, JsonSerializerOptions.Web);

                if (result?.CurrentChildId is Guid currentChildId)
                {
                    _logger.LogInformation("Контекст получен: ChildId={ChildId}, HasContext={HasContext}",
                        currentChildId, result.HasContext);
                    return currentChildId;
                }

                // После привязки кода старые версии бота не сохраняли активного ребёнка.
                // Если у пользователя ровно один ребёнок, выбор однозначен и безопасен.
                var linkedChildren = await GetLinkedChildrenAsync(telegramUserId, cancellationToken);
                if (linkedChildren.Count == 1)
                {
                    var childId = linkedChildren[0].ChildId;
                    _ = await SetCurrentChildIdAsync(telegramUserId, childId, cancellationToken);
                    _logger.LogInformation(
                        "Автоматически выбран единственный ребёнок {ChildId} для Telegram {TelegramUserId}",
                        childId,
                        telegramUserId);
                    return childId;
                }

                if (result != null)
                {
                    _logger.LogInformation("Контекст Telegram {TelegramUserId} не содержит активного ребёнка", telegramUserId);
                    return null;
                }
            }

            _logger.LogWarning("Ошибка API при получении контекста: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении контекста для Telegram пользователя {TelegramUserId}", telegramUserId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetCurrentChildIdAsync(long telegramUserId, Guid? childId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Установка ChildId={ChildId} для Telegram пользователя {TelegramUserId}",
                childId, telegramUserId);

            var request = new SetBotUserContextRequest
            {
                ChildId = childId
            };

            var json = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"/api/bot-service/context/{telegramUserId}", content, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Контекст успешно установлен для Telegram пользователя {TelegramUserId}", telegramUserId);
                return true;
            }

            _logger.LogWarning("Ошибка API при установке контекста: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при установке контекста для Telegram пользователя {TelegramUserId}", telegramUserId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<List<ChildSummaryBot>> GetLinkedChildrenAsync(long telegramUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Получение списка привязанных детей для Telegram пользователя {TelegramUserId}", telegramUserId);

            var response = await _httpClient.GetAsync($"/api/bot-service/context/{telegramUserId}/children", cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<LinkedChildrenResponse>(responseJson, JsonSerializerOptions.Web);

                if (result != null)
                {
                    _logger.LogInformation("Получено {Count} привязанных детей", result.TotalChildren);
                    return result.Children;
                }
            }

            _logger.LogWarning("Ошибка API при получении списка детей: {StatusCode}", response.StatusCode);
            return new List<ChildSummaryBot>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении списка детей для Telegram пользователя {TelegramUserId}", telegramUserId);
            return new List<ChildSummaryBot>();
        }
    }

    /// <inheritdoc />
    public async Task<bool?> GetDailySummaryEnabledAsync(long telegramUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/bot-service/context/{telegramUserId}/daily-summary",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<BotDailySummaryPreferenceResponse>(json, JsonSerializerOptions.Web)?.Enabled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось получить настройку ежедневной сводки для Telegram {TelegramUserId}", telegramUserId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetDailySummaryEnabledAsync(long telegramUserId, bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new BotDailySummaryPreferenceRequest { Enabled = enabled }, JsonSerializerOptions.Web),
                Encoding.UTF8,
                "application/json");
            var response = await _httpClient.PutAsync(
                $"/api/bot-service/context/{telegramUserId}/daily-summary",
                content,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось сохранить настройку ежедневной сводки для Telegram {TelegramUserId}", telegramUserId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ClearContextAsync(long telegramUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Очистка контекста для Telegram пользователя {TelegramUserId}", telegramUserId);

            // Очистка контекста = установка ChildId в null
            return await SetCurrentChildIdAsync(telegramUserId, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке контекста для Telegram пользователя {TelegramUserId}", telegramUserId);
            return false;
        }
    }
}

/// <summary>
/// Запрос на установку контекста пользователя бота
/// </summary>
internal class SetBotUserContextRequest
{
    public Guid? ChildId { get; set; }
}

/// <summary>
/// Ответ с контекстом пользователя бота
/// </summary>
internal class BotUserContextResponse
{
    public long TelegramUserId { get; set; }
    public Guid? CurrentChildId { get; set; }
    public bool HasContext { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

/// <summary>
/// Ответ со списком привязанных детей
/// </summary>
internal class LinkedChildrenResponse
{
    public long TelegramUserId { get; set; }
    public List<ChildSummaryBot> Children { get; set; } = new();
    public int TotalChildren { get; set; }
}

internal class BotDailySummaryPreferenceRequest
{
    public bool Enabled { get; set; }
}

internal class BotDailySummaryPreferenceResponse
{
    public bool Enabled { get; set; }
}
