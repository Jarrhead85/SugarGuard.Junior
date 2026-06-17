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
    public async Task<GigaChatResponse> GetRecommendationAsync(GigaChatRequest request)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Сначала пытаемся получить рекомендацию от GigaChat
            var gigaChatResponse = await GetGigaChatRecommendationAsync(request);
            
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

    /// <summary>
    /// Получить токен для GigaChat
    /// </summary>
    public Task<string?> GetAccessTokenAsync() =>
        _tokenCache.GetOrRefreshAsync(RequestNewTokenAsync);

    /// <summary>
    /// Запрос нового токена у GigaChat OAuth
    /// </summary>
    private async Task<string?> RequestNewTokenAsync()
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

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
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
    private async Task<GigaChatResponse> GetGigaChatRecommendationAsync(GigaChatRequest request)
    {
        var accessToken = await GetAccessTokenAsync();
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
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 200
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        httpRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), 
            Encoding.UTF8, 
            "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
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
                        Urgency = DetermineUrgency(request.GlucoseStatus)
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
        var snacksText = request.AvailableSnacks.Any() 
            ? string.Join(", ", request.AvailableSnacks)
            : "рюкзак пуст";

        var recentValuesText = request.RecentGlucoseValues.Any()
            ? string.Join(" → ", request.RecentGlucoseValues.Select(v => v.ToString("F1")))
            : "нет данных";

        return $"""
            Ребёнку {request.ChildAge} лет, диабет {request.DiabetesType}.
            Текущий уровень глюкозы: {request.CurrentGlucose:F1} ммоль/л ({request.GlucoseStatus}).
            В последние 3 часа: {recentValuesText} (тренд {request.Trend}).
            Целевой диапазон: {request.TargetRangeMin:F1}-{request.TargetRangeMax:F1}.
            Используемый инсулин: {request.InsulinScheme}.
            Чувствительность: 1 ед на {request.InsulinSensitivity:F1} ммоль/л.
            Рюкзак (доступные перекусы): {snacksText}.
            
            Дай краткий совет на русском языке (1-2 предложения).
            ВАЖНО: Это не медицинская консультация, а помощь!
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
                    ? $"Низкий сахар. Съешь что-то сладкое из рюкзака: {string.Join(", ", request.AvailableSnacks.Take(2))}."
                    : "Низкий сахар. Съешь быстрые углеводы (сок, фрукт, конфету).";
                urgency = "HIGH";
                break;

            case "ВЫСОКО":
                recommendationText = "Повышенный сахар. Проверь, не пора ли сделать инсулин. Пей больше воды.";
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
