// Реализация сервиса для работы с GigaChat API
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для работы с GigaChat API от Сбера
/// Реализует получение access token через OAuth и отправку запросов на рекомендации
/// </summary>
public class GigaChatService : IGigaChatService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GigaChatService> _logger;
    
    // Настройки GigaChat API
    private const string BaseUrl = "https://gigachat.devices.sberbank.ru/api/v1";
    private const string AuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
    private const int TimeoutSeconds = 5;
    
    // Кэш токена (thread-safe через SemaphoreSlim)
    private string? _accessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    
    // Учетные данные (в реальном приложении должны быть в конфигурации)
    private readonly string? _clientId;
    private readonly string? _clientSecret;

    public GigaChatService(IHttpClientFactory httpClientFactory, ILogger<GigaChatService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GigaChat");
        _logger = logger;

        _clientId = Environment.GetEnvironmentVariable("GIGACHAT_CLIENT_ID");
        _clientSecret = Environment.GetEnvironmentVariable("GIGACHAT_CLIENT_SECRET");

        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
        {
            _logger.LogWarning(
                "GIGACHAT_CLIENT_ID/SECRET is not set. AI recommendations will fail until configured.");
        }
    }

    /// <summary>
    /// Получает рекомендацию от GigaChat
    /// </summary>
    public async Task<GigaChatResponse?> GetRecommendationAsync(GigaChatRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Запрос рекомендации от GigaChat для ребёнка {ChildId}, глюкоза {Glucose}", 
                request.ChildId, request.CurrentGlucose);

            // Получаем access token
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Не удалось получить access token для GigaChat");
                return CreateErrorResponse("Не удалось авторизоваться в GigaChat", stopwatch.ElapsedMilliseconds);
            }

            // Формируем промпт
            var prompt = BuildPrompt(request);
            
            // Отправляем запрос к GigaChat
            var chatRequest = new
            {
                model = "GigaChat",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.3, // Низкая температура для более предсказуемых ответов
                max_tokens = 200   // Ограничиваем длину ответа
            };

            var json = JsonSerializer.Serialize(chatRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions")
            {
                Content = content,
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
            };
            
            var response = await _httpClient.SendAsync(httpRequest);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GigaChat API вернул ошибку: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                return CreateErrorResponse($"Ошибка API: {response.StatusCode}", stopwatch.ElapsedMilliseconds);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<GigaChatApiResponse>(responseJson);
            
            if (chatResponse?.Choices?.Length > 0)
            {
                var recommendationText = chatResponse.Choices[0].Message?.Content ?? "";
                var urgency = DetermineUrgency(request.CurrentGlucose);
                
                _logger.LogInformation("Получена рекомендация от GigaChat за {Latency}ms", 
                    stopwatch.ElapsedMilliseconds);
                
                return new GigaChatResponse
                {
                    Success = true,
                    RecommendationText = recommendationText,
                    Urgency = urgency,
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                    ModelUsed = "GigaChat"
                };
            }
            
            _logger.LogWarning("GigaChat вернул пустой ответ");
            return CreateErrorResponse("Пустой ответ от GigaChat", stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Таймаут запроса к GigaChat ({Timeout}s)", TimeoutSeconds);
            return CreateErrorResponse("Таймаут запроса", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при запросе к GigaChat");
            return CreateErrorResponse($"Ошибка: {ex.Message}", stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Получает access token для работы с GigaChat API
    /// Polly автоматически обрабатывает retry при ошибках (если настроен)
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
        {
            _logger.LogError(
                "Cannot acquire GigaChat token: GIGACHAT_CLIENT_ID or GIGACHAT_CLIENT_SECRET not set.");
            return null;
        }

        // Быстрый путь без блокировки
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiresAt)
        {
            _logger.LogDebug("Using cached GigaChat token (expires in {Seconds}s)",
                (_tokenExpiresAt - DateTime.UtcNow).TotalSeconds);
            return _accessToken;
        }

        await _tokenLock.WaitAsync();
        try
        {
            // Double-check: другой поток мог уже получить токен
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiresAt)
            {
                _logger.LogDebug("Token refreshed by another thread, using cached");
                return _accessToken;
            }

            _logger.LogInformation("Запрос нового GigaChat access token");

            var authRequest = new { scope = "GIGACHAT_API_PERS" };
            var json = JsonSerializer.Serialize(authRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Используем HttpRequestMessage вместо DefaultRequestHeaders (H-2)
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, AuthUrl)
            {
                Content = content,
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) }
            };

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var authResponse = JsonSerializer.Deserialize<GigaChatAuthResponse>(responseJson);

                if (!string.IsNullOrEmpty(authResponse?.AccessToken))
                {
                    _accessToken = authResponse.AccessToken;
                    _tokenExpiresAt = DateTime.UtcNow.AddSeconds(authResponse.ExpiresIn - 60);

                    _logger.LogInformation("GigaChat token получен (истекает через {Minutes} мин)",
                        authResponse.ExpiresIn / 60);

                    return _accessToken;
                }
            }

            _logger.LogError("Failed to obtain GigaChat token: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obtaining GigaChat token");
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Проверяет доступность GigaChat API
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var token = await GetAccessTokenAsync();
            return !string.IsNullOrEmpty(token);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Формирует промпт для GigaChat на основе данных о ребёнке
    /// </summary>
    private static string BuildPrompt(GigaChatRequest request)
    {
        var trendText = request.Trend switch
        {
            "rising" => "растёт",
            "falling" => "падает",
            "stable" => "стабильна",
            _ => "неизвестен"
        };

        var recentValuesText = request.RecentGlucoseValues.Count > 0 
            ? string.Join(" → ", request.RecentGlucoseValues.Select(v => v.ToString("F1")))
            : "нет данных";

        var snacksText = request.AvailableSnacks.Count > 0
            ? string.Join(", ", request.AvailableSnacks)
            : "рюкзак пуст";

        return $"""
            Ребёнку {request.ChildAge} лет, диабет {request.DiabetesType}.
            Текущий уровень глюкозы: {request.CurrentGlucose:F1} ммоль/л ({request.GlucoseStatus}).
            В последние 3 часа: {recentValuesText} (тренд {trendText}).
            Целевой диапазон: {request.TargetRangeMin:F1}-{request.TargetRangeMax:F1} ммоль/л.
            Используемый инсулин: {request.InsulinScheme}.
            Чувствительность: 1 ед на {request.InsulinSensitivity:F1} ммоль/л.
            Рюкзак (доступные перекусы): {snacksText}.
            
            Дай краткий совет на русском языке (1-2 предложения).
            ВАЖНО: Это не медицинская консультация, а помощь!
            """;
    }

    /// <summary>
    /// Определяет уровень срочности на основе уровня глюкозы
    /// </summary>
    private static string DetermineUrgency(double glucose)
    {
        var status = GlucoseClassifier.Classify(glucose);
        return status switch
        {
            Models.Enums.GlucoseStatus.CriticallyLow => "Critical",
            Models.Enums.GlucoseStatus.CriticallyHigh => "Critical",
            Models.Enums.GlucoseStatus.Low => "Warning",
            Models.Enums.GlucoseStatus.High => "Warning",
            _ => "Normal"
        };
    }

    /// <summary>
    /// Создаёт ответ об ошибке
    /// </summary>
    private static GigaChatResponse CreateErrorResponse(string errorMessage, long latencyMs)
    {
        return new GigaChatResponse
        {
            Success = false,
            ErrorMessage = errorMessage,
            LatencyMs = latencyMs,
            ModelUsed = "GigaChat"
        };
    }

    /// <summary>
    /// Модель ответа от GigaChat API для авторизации
    /// </summary>
    private class GigaChatAuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    /// <summary>
    /// Модель ответа от GigaChat API для чата
    /// </summary>
    private class GigaChatApiResponse
    {
        public GigaChatChoice[]? Choices { get; set; }
    }

    private class GigaChatChoice
    {
        public GigaChatMessage? Message { get; set; }
    }

    private class GigaChatMessage
    {
        public string? Content { get; set; }
    }
}
