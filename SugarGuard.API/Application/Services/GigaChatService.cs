using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SugarGuard.API.Data;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Сервис для работы с GigaChat API
/// </summary>
public class GigaChatService : IGigaChatService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GigaChatService> _logger;
    private readonly AppDbContext _context;
    private readonly IGigaChatTokenCache _tokenCache;

    public GigaChatService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GigaChatService> logger,
        AppDbContext context,
        IGigaChatTokenCache tokenCache)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _context = context;
        _tokenCache = tokenCache;
    }

    /// <summary>
    /// Получить рекомендацию от GigaChat
    /// </summary>
    public async Task<GigaChatResponse> GetRecommendationAsync(
        GigaChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Критические диапазоны не должны зависеть от внешней модели.
        // Модель остаётся полезной только для обычных, неэкстренных подсказок.
        var safetyResponse = GetSafetyRecommendation(request);
        if (safetyResponse is not null)
        {
            safetyResponse.LatencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            return safetyResponse;
        }
        
        try
        {
            var gigaChatResponse = await GetGigaChatRecommendationAsync(request, cancellationToken);
            
            if (gigaChatResponse.IsSuccess)
            {
                var latency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                gigaChatResponse.LatencyMs = latency;
                return gigaChatResponse;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при обращении к GigaChat для ребёнка {ChildId}, используем локальную рекомендацию", request.ChildId);
        }

        var localResponse = GetLocalRecommendation(request);
        var totalLatency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        localResponse.LatencyMs = totalLatency;
        
        return localResponse;
    }

    private static GigaChatResponse? GetSafetyRecommendation(GigaChatRequest request)
    {
        if (request.CurrentGlucose <= 3.9)
        {
            var backpackAdvice = request.AvailableSnacks.Any()
                ? $"В рюкзаке сейчас есть: {string.Join(", ", request.AvailableSnacks.Take(2))}. Используй быстрые углеводы по своему плану и покажи взрослому, что выбрал."
                : "Подходящего перекуса в рюкзаке не видно. Позови взрослого и используй аварийный запас быстрых углеводов по своему плану.";

            return new GigaChatResponse
            {
                RecommendationText = request.CurrentGlucose < 3.1
                    ? $"Глюкоза критически низкая. Немедленно позови взрослого. {backpackAdvice} Повтори измерение через 10–15 минут."
                    : $"Глюкоза низкая. Позови взрослого. {backpackAdvice} Повтори измерение через 10–15 минут.",
                ModelUsed = "SafetyRules",
                IsLocalFallback = true,
                IsSuccess = true,
                Urgency = request.CurrentGlucose < 3.1 ? "CRITICAL" : "HIGH"
            };
        }

        if (request.CurrentGlucose >= 14.0)
        {
            return new GigaChatResponse
            {
                RecommendationText = "Глюкоза очень высокая. Сразу сообщи взрослому, пей воду и проверь кетоны по своему плану; при тошноте, рвоте или сильной слабости нужна срочная медицинская помощь.",
                ModelUsed = "SafetyRules",
                IsLocalFallback = true,
                IsSuccess = true,
                Urgency = request.CurrentGlucose > 15.0 ? "CRITICAL" : "HIGH"
            };
        }

        return null;
    }

    /// <summary>
    /// Получить токен для GigaChat
    /// </summary>
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
        _tokenCache.GetOrRefreshAsync(() => RequestNewTokenAsync(cancellationToken));

    /// <summary>
    /// Запрос нового токена у GigaChat OAuth
    /// </summary>
    private async Task<string?> RequestNewTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var clientId = _configuration["GigaChat:ClientId"];
            var clientSecret = _configuration["GigaChat:ClientSecret"];
            var authUrl = _configuration["GigaChat:AuthUrl"] ?? "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                _logger.LogWarning("GigaChat credentials не настроены");
                return null;
            }

            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            
            var request = new HttpRequestMessage(HttpMethod.Post, authUrl);
            request.Headers.Add("Authorization", $"Basic {authString}");
            request.Headers.Add("RqUID", Guid.NewGuid().ToString());
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
            });

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var tokenResponse = JsonSerializer.Deserialize<GigaChatTokenResponse>(responseContent);
                
                if (tokenResponse?.AccessToken != null)
                {
                    _logger.LogInformation("GigaChat token obtained (expires in {Minutes}m)",
                        tokenResponse.ExpiresIn / 60);
                    return tokenResponse.AccessToken;
                }
            }
            
            _logger.LogError("Failed to obtain GigaChat token: {StatusCode}",
                response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obtaining GigaChat token");
            return null;
        }
    }

    /// <summary>
    /// Получить рекомендацию непосредственно от GigaChat API
    /// </summary>
    private async Task<GigaChatResponse> GetGigaChatRecommendationAsync(
        GigaChatRequest request,
        CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(accessToken))
        {
            return new GigaChatResponse
            {
                IsSuccess = false,
                ErrorMessage = "Не удалось получить access token"
            };
        }

        var prompt = BuildPrompt(request);
        var apiUrl = _configuration["GigaChat:ApiUrl"] ?? "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

        var requestBody = new
        {
            model = "GigaChat",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "Ты детский помощник по диабету SugarGuard. Отвечай по-русски, спокойно, кратко и понятно ребёнку. Не назначай новую дозу, не меняй дозу или схему инсулина, не заменяй врача, не скрывай критичность ситуации. Можно объяснять факты, напоминать утверждённый план, просить сообщить взрослому и перечислять данные, которые стоит проверить."
                },
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = 90
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        httpRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), 
            Encoding.UTF8, 
            "application/json");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var gigaChatResponse = JsonSerializer.Deserialize<GigaChatApiResponse>(responseContent);
            
            if (gigaChatResponse?.Choices?.Length > 0)
            {
                var recommendationText = gigaChatResponse.Choices[0].Message?.Content?.Trim();
                
                if (!string.IsNullOrEmpty(recommendationText))
                {
                    return new GigaChatResponse
                    {
                        RecommendationText = recommendationText,
                        ModelUsed = "GigaChat",
                        IsSuccess = true,
                        Urgency = DetermineUrgency(request.GlucoseStatus),
                        InputTokens = gigaChatResponse.Usage?.PromptTokens,
                        OutputTokens = gigaChatResponse.Usage?.CompletionTokens,
                        TotalTokens = gigaChatResponse.Usage?.TotalTokens
                    };
                }
            }
        }

        return new GigaChatResponse
        {
            IsSuccess = false,
            ErrorMessage = $"Ошибка API: {response.StatusCode}"
        };
    }

    /// <summary>
    /// Сформировать промпт для GigaChat на основе данных ребёнка
    /// </summary>
    private string BuildPrompt(GigaChatRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.StructuredContextJson))
        {
            return $"""
                Вопрос пользователя: {request.Question}

                Ниже структурированный обезличенный контекст SugarGuard. В нём нет ФИО и контактов.
                Используй только эти данные. Не придумывай еду, инсулин, симптомы или назначения.
                Перед ответом обязательно проверь:
                - current.measurement, dailySummary и longTermPatterns: текущая глюкоза, тренд и статистика;
                - current.lastMeal и recentHistory.nutrition: что и когда ребёнок ел, ХЕ и перекусы;
                - current.lastInsulin и recentHistory.insulin: фактически введённый инсулин;
                - availableBackpack: что реально доступно ребёнку в рюкзаке сейчас;
                - recentHistory.consumedBackpackSnacks: какие перекусы из рюкзака уже были съедены недавно.
                Если в контексте есть питание, перекусы, рюкзак или статистика глюкозы, ответ должен явно учитывать эти факты.
                Если подходящего перекуса в рюкзаке нет или данных мало, прямо скажи об этом и попроси обратиться к взрослому.
                Если данных мало, прямо скажи, что вывод осторожный.

                {request.StructuredContextJson}
                """;
        }

        var snacksText = request.AvailableSnacks.Any() 
            ? string.Join(", ", request.AvailableSnacks)
            : "рюкзак пуст";

        var recentValuesText = request.RecentGlucoseValues.Any()
            ? string.Join(" → ", request.RecentGlucoseValues.Select(v => v.ToString("F1")))
            : "нет данных";

        return $"""
            Возраст: {request.ChildAge}; диабет: {request.DiabetesType}.
            Глюкоза: {request.CurrentGlucose:F1} ммоль/л ({request.GlucoseStatus}); тренд: {request.Trend}; недавние: {recentValuesText}.
            Цель: {request.TargetRangeMin:F1}-{request.TargetRangeMax:F1}.
            Рюкзак: {snacksText}.
            Дай одно конкретное безопасное действие. При низкой глюкозе предложи только реально доступный перекус; если подходящего нет, скажи обратиться к взрослому и взять быстрые углеводы из аварийного запаса.
            """;
    }

    /// <summary>
    /// Получить локальную рекомендацию на основе правил
    /// </summary>
    private GigaChatResponse GetLocalRecommendation(GigaChatRequest request)
    {
        string recommendationText;
        string urgency;

        switch (request.GlucoseStatus.ToUpper())
        {
            case "КРИТИЧЕСКИ":
                if (request.CurrentGlucose < 3.1)
                {
                    recommendationText = "КРИТИЧЕСКИ НИЗКИЙ уровень! Срочно съешь быстрые углеводы (сок, конфету). Обратись к взрослым!";
                    urgency = "CRITICAL";
                }
                else
                {
                    recommendationText = "КРИТИЧЕСКИ ВЫСОКИЙ уровень! Проверь кетоны, обратись к врачу. Не ешь углеводы без инсулина.";
                    urgency = "CRITICAL";
                }
                break;

            case "НИЗКО":
                recommendationText = request.AvailableSnacks.Any()
                    ? $"Низкий сахар. Выбери из рюкзака: {string.Join(", ", request.AvailableSnacks.Take(2))}."
                    : "Низкий сахар. Позови взрослого и используй аварийный запас быстрых углеводов.";
                urgency = "HIGH";
                break;

            case "ВЫСОКО":
                recommendationText = "Повышенный сахар. Сообщи взрослому, пей воду и действуй по своему плану коррекции. Не ешь дополнительные углеводы.";
                urgency = "MEDIUM";
                break;

            default: // НОРМА
                recommendationText = request.AvailableSnacks.Any()
                    ? "Отличный уровень! Можешь перекусить из рюкзака, если голоден."
                    : "Отличный уровень сахара! Продолжай в том же духе.";
                urgency = "LOW";
                break;
        }

        return new GigaChatResponse
        {
            RecommendationText = recommendationText,
            ModelUsed = "Local",
            IsLocalFallback = true,
            IsSuccess = true,
            Urgency = urgency
        };
    }

    /// <summary>
    /// Определить уровень срочности на основе статуса глюкозы
    /// </summary>
    private string DetermineUrgency(string glucoseStatus)
    {
        return glucoseStatus.ToUpper() switch
        {
            "КРИТИЧЕСКИ" => "CRITICAL",
            "НИЗКО" => "HIGH",
            "ВЫСОКО" => "MEDIUM",
            _ => "LOW"
        };
    }
}

/// <summary>
/// Ответ от GigaChat OAuth API
/// </summary>
internal class GigaChatTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

/// <summary>
/// Ответ от GigaChat Chat Completions API
/// </summary>
internal class GigaChatApiResponse
{
    [JsonPropertyName("choices")]
    public GigaChatChoice[]? Choices { get; set; }

    [JsonPropertyName("usage")]
    public GigaChatUsage? Usage { get; set; }
}

internal class GigaChatChoice
{
    [JsonPropertyName("message")]
    public GigaChatMessage? Message { get; set; }
}

internal class GigaChatMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

internal class GigaChatUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int? PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; set; }
}
