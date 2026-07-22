using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using SugarGuard.API.Application.Ai;
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
                    content = "Ты детский помощник по диабету SugarGuard. Отвечай по-русски, спокойно, кратко и понятно ребёнку. Всегда опирайся на факты из контекста: текущую глюкозу, цель, недавнюю динамику, последнюю еду/перекус, последний инсулин, статистику дня и содержимое рюкзака. Не назначай новую дозу, не меняй дозу или схему инсулина, не заменяй врача, не скрывай критичность ситуации. Если советуешь еду или перекус, называй только то, что реально есть в рюкзаке; если подходящего нет, так и скажи. Можно объяснять факты, напоминать утверждённый план, просить сообщить взрослому и перечислять данные, которые стоит проверить."
                },
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = 240
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
            var clinicalDigest = BuildClinicalDigest(request);

            return $"""
                Вопрос пользователя: {request.Question}

                Краткий клинический контекст SugarGuard без ФИО и контактов:
                {clinicalDigest}

                Правила ответа:
                1. Дай одно безопасное действие на ближайшие минуты, а не общий медицинский текст.
                2. Строка "Профиль" задаёт тип диабета и возрастную группу. Для СД1 учитывай, что возможна инсулинотерапия, но не предлагай дозу или коррекцию. Для СД2 не описывай инсулин как обязательный элемент лечения и советуй питание с учётом назначенного врачом плана. При любом типе диабета при низкой глюкозе приоритет — безопасное устранение гипогликемии и обращение ко взрослому.
                3. В ответе явно учти рюкзак, последнюю еду/перекус, последний инсулин и статистику глюкозы, если они есть в контексте.
                4. Строка "Рюкзак сейчас" — единственный разрешённый список доступной еды. Нельзя писать "из рюкзака" рядом с продуктом, которого нет в этой строке. Нельзя заменять отсутствующий продукт похожим: булочка, шоколадка, конфета, сок, хлебцы, печенье, банан или йогурт допустимы только если они прямо указаны в "Рюкзак сейчас".
                5. При нормальной глюкозе не предлагай есть просто "на всякий случай"; можно сказать продолжать день и наблюдать самочувствие.
                6. При повышенной глюкозе не советуй дополнительные углеводы; попроси сообщить взрослому, пить воду и действовать по утверждённому плану.
                7. Не рассчитывай и не назначай дозу инсулина. Если нужна коррекция, скажи действовать только по плану с взрослым.
                8. Если подходящего предмета в рюкзаке нет, прямо скажи: "в рюкзаке подходящего перекуса не вижу".
                9. Ответ: 2-4 коротких предложения, понятных ребёнку. Без списков, без длинных дисклеймеров.
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

    private static string BuildClinicalDigest(GigaChatRequest request)
    {
        ClinicalContext? context = null;

        try
        {
            context = JsonSerializer.Deserialize<ClinicalContext>(
                request.StructuredContextJson!,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            // Context format can evolve independently from the prompt. If parsing fails,
            // keep the request usable instead of dropping the whole AI flow.
        }

        if (context is null)
        {
            return TrimForPrompt(request.StructuredContextJson!, 3500);
        }

        var lines = new[]
        {
            BuildProfileLine(context, request),
            BuildCurrentLine(context, request),
            BuildDailySummaryLine(context),
            BuildLastMealLine(context),
            BuildLastInsulinLine(context),
            BuildBackpackLine(context),
            BuildConsumedBackpackLine(context),
            BuildRecentMeasurementsLine(context),
            BuildRecentNutritionLine(context),
            BuildLongTermPatternsLine(context),
            BuildConversationLine(context)
        };

        return TrimForPrompt(
            string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line))),
            6500);
    }

    private static string BuildProfileLine(ClinicalContext context, GigaChatRequest request)
    {
        var diabetesType = string.IsNullOrWhiteSpace(request.DiabetesType)
            ? FormatDiabetesType(context.Profile.DiabetesType)
            : request.DiabetesType;
        var insulinScheme = string.IsNullOrWhiteSpace(context.Profile.InsulinScheme)
            ? "не указана"
            : context.Profile.InsulinScheme;

        return $"Профиль: {context.Profile.AgeGroup}, диабет {diabetesType}, схема инсулина: {insulinScheme}.";
    }

    private static string FormatDiabetesType(string diabetesType) => diabetesType switch
    {
        "Type1" => "1 типа",
        "Type2" => "2 типа",
        _ => string.IsNullOrWhiteSpace(diabetesType) ? "не указан" : diabetesType
    };

    private static string BuildCurrentLine(ClinicalContext context, GigaChatRequest request)
    {
        var measurement = context.Current.Measurement;
        var value = measurement?.Value ?? Convert.ToDecimal(request.CurrentGlucose);
        var status = string.IsNullOrWhiteSpace(request.GlucoseStatus) ? "не указан" : request.GlucoseStatus;
        var trend = string.IsNullOrWhiteSpace(request.Trend) ? "нет данных" : request.Trend;
        var state = string.IsNullOrWhiteSpace(measurement?.State)
            ? "самочувствие не указано"
            : measurement.State;

        return $"Сейчас: глюкоза {Format(value)} ммоль/л, статус {status}, тренд {trend}, цель {Format(context.Profile.TargetRangeMin)}-{Format(context.Profile.TargetRangeMax)} ммоль/л, {state}.";
    }

    private static string BuildDailySummaryLine(ClinicalContext context)
    {
        var summary = context.DailySummary;
        if (summary.MeasurementCount <= 0)
        {
            return "Статистика дня: измерений пока нет.";
        }

        var average = summary.AverageGlucose.HasValue ? Format(summary.AverageGlucose.Value) : "нет";
        var min = summary.MinGlucose.HasValue ? Format(summary.MinGlucose.Value) : "нет";
        var max = summary.MaxGlucose.HasValue ? Format(summary.MaxGlucose.Value) : "нет";
        var timeInRange = summary.TimeInRangePercent.HasValue ? $"{Format(summary.TimeInRangePercent.Value)}%" : "нет";

        return $"Статистика дня: {summary.MeasurementCount} измер., средняя {average}, мин/макс {min}/{max}, в цели {timeInRange}, низких {summary.LowEpisodes}, высоких {summary.HighEpisodes}, еда {Format(summary.TotalBreadUnits)} ХЕ, инсулин {Format(summary.TotalInsulinUnits)} ед.";
    }

    private static string BuildLastMealLine(ClinicalContext context)
    {
        var meal = context.Current.LastMeal;
        if (meal is null)
        {
            return "Последняя еда/перекус: данных нет.";
        }

        var name = string.IsNullOrWhiteSpace(meal.MealName)
            ? meal.MealType
            : $"{meal.MealType} ({meal.MealName})";
        var minutes = context.Current.MinutesSinceMeal.HasValue
            ? $"{context.Current.MinutesSinceMeal.Value} мин назад"
            : "время не рассчитано";

        return $"Последняя еда/перекус: {name}, {Format(meal.BreadUnits)} ХЕ, {minutes}.";
    }

    private static string BuildLastInsulinLine(ClinicalContext context)
    {
        var insulin = context.Current.LastInsulin;
        if (insulin is null)
        {
            return "Последний инсулин: данных нет.";
        }

        var minutes = context.Current.MinutesSinceInsulin.HasValue
            ? $"{context.Current.MinutesSinceInsulin.Value} мин назад"
            : "время не рассчитано";

        return $"Последний инсулин: {Format(insulin.Units)} ед. ({insulin.MealType}), {minutes}.";
    }

    private static string BuildBackpackLine(ClinicalContext context)
    {
        if (context.AvailableBackpack.Count == 0)
        {
            return "Рюкзак сейчас: пуст или данных нет.";
        }

        var snacks = context.AvailableBackpack
            .GroupBy(item => new { item.SnackName, item.BreadUnits })
            .OrderBy(group => group.Key.SnackName)
            .ThenBy(group => group.Key.BreadUnits)
            .Select(group => group.Count() == 1
                ? $"{group.Key.SnackName} ({Format(group.Key.BreadUnits)} ХЕ)"
                : $"{group.Key.SnackName}: {group.Count()} шт. по {Format(group.Key.BreadUnits)} ХЕ");

        return $"Рюкзак сейчас: {string.Join("; ", snacks)}.";
    }

    private static string BuildConsumedBackpackLine(ClinicalContext context)
    {
        if (context.RecentHistory.ConsumedBackpackSnacks.Count == 0)
        {
            return "Недавно съедено из рюкзака: нет записей.";
        }

        var consumed = context.RecentHistory.ConsumedBackpackSnacks
            .OrderByDescending(item => item.RecordedAt)
            .Take(4)
            .Select(item => $"{item.SnackName} ({Format(item.BreadUnits)} ХЕ, {item.RecordedAt:dd.MM HH:mm})");

        return $"Недавно съедено из рюкзака: {string.Join("; ", consumed)}.";
    }

    private static string BuildRecentMeasurementsLine(ClinicalContext context)
    {
        if (context.RecentHistory.Measurements.Count == 0)
        {
            return "Недавние измерения: нет данных.";
        }

        var measurements = context.RecentHistory.Measurements
            .OrderByDescending(item => item.MeasuredAt)
            .Take(6)
            .OrderBy(item => item.MeasuredAt)
            .Select(item => $"{item.MeasuredAt:HH:mm}={Format(item.Value)}");

        return $"Недавние измерения: {string.Join(" → ", measurements)}.";
    }

    private static string BuildRecentNutritionLine(ClinicalContext context)
    {
        if (context.RecentHistory.Nutrition.Count == 0)
        {
            return "Недавнее питание: нет записей.";
        }

        var nutrition = context.RecentHistory.Nutrition
            .OrderByDescending(item => item.RecordedAt)
            .Take(5)
            .Select(item =>
            {
                var name = string.IsNullOrWhiteSpace(item.MealName)
                    ? item.MealType
                    : $"{item.MealType} {item.MealName}";
                return $"{item.RecordedAt:HH:mm}: {name}, {Format(item.BreadUnits)} ХЕ";
            });

        return $"Недавнее питание: {string.Join("; ", nutrition)}.";
    }

    private static string BuildLongTermPatternsLine(ClinicalContext context)
    {
        var observations = context.LongTermPatterns.Observations
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(3)
            .ToList();

        if (observations.Count == 0)
        {
            return $"Динамика за {context.LongTermPatterns.PeriodDays} дней: {context.LongTermPatterns.DataQuality}.";
        }

        return $"Динамика за {context.LongTermPatterns.PeriodDays} дней: {context.LongTermPatterns.DataQuality}; {string.Join(" ", observations)}";
    }

    private static string BuildConversationLine(ClinicalContext context)
    {
        var summary = TrimForPrompt(
            context.Conversation.Summary.ReplaceLineEndings(" ").Trim(),
            450);

        if (context.Conversation.RecentMessages.Count == 0)
        {
            return string.IsNullOrWhiteSpace(summary)
                ? "Память диалога: нет предыдущих сообщений."
                : $"Краткое резюме предыдущего диалога: {summary}";
        }

        var messages = context.Conversation.RecentMessages
            .OrderByDescending(item => item.CreatedAt)
            .Take(4)
            .OrderBy(item => item.CreatedAt)
            .Select(item => $"{item.Role}: {TrimForPrompt(item.Text.ReplaceLineEndings(" "), 160)}");

        var history = $"Недавний диалог: {string.Join(" | ", messages)}.";
        return string.IsNullOrWhiteSpace(summary)
            ? history
            : $"Краткое резюме предыдущего диалога: {summary}. {history}";
    }

    private static string Format(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string TrimForPrompt(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

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
