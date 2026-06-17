using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace SugarGuard.Bot.Services;

/// <summary>
/// Клиент для взаимодействия с SugarGuard API.
/// <para>
/// зарегистрирован как Singleton (раньше — Transient из-за
/// <c>AddHttpClient&lt;ApiClient&gt;()</c>). Mutable state (<c>_accessToken</c>,
/// <c>_accessTokenExpiresAtUtc</c>) теперь безопасно шарится между concurrent
/// вызовами через <see cref="SemaphoreSlim"/> + <see cref="Volatile.Read{T}"/>.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;
    private readonly string _baseUrl;
    private readonly string? _botApiKey;

    private string? _accessToken;
    private long _accessTokenExpiresAtTicks; // DateTime.Ticks (UTC)

    private readonly SemaphoreSlim _loginLock = new(1, 1);

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["BotSettings:ApiUrl"] ?? "https://localhost:7001";
        _botApiKey = Environment.GetEnvironmentVariable("BOT_SERVICE_AUTH_KEY")
            ?? configuration["BotSettings:ApiKey"];

        // Настраиваем HttpClient
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SugarGuard-Bot/1.0");
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_botApiKey))
        {
            return;
        }

        var currentToken = Volatile.Read(ref _accessToken);
        var currentExpiresAt = new DateTime(Volatile.Read(ref _accessTokenExpiresAtTicks), DateTimeKind.Utc);
        if (!string.IsNullOrWhiteSpace(currentToken) &&
            currentExpiresAt > DateTime.UtcNow.AddMinutes(1))
        {
            ApplyAuthorizationHeader(currentToken);
            return;
        }

        await _loginLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check: другой поток мог уже залогиниться, пока ждали семафор.
            currentToken = Volatile.Read(ref _accessToken);
            currentExpiresAt = new DateTime(Volatile.Read(ref _accessTokenExpiresAtTicks), DateTimeKind.Utc);
            if (!string.IsNullOrWhiteSpace(currentToken) &&
                currentExpiresAt > DateTime.UtcNow.AddMinutes(1))
            {
                ApplyAuthorizationHeader(currentToken);
                return;
            }

            var loginBody = JsonSerializer.Serialize(new { apiKey = _botApiKey }, JsonSerializerOptions.Web);
            using var loginContent = new StringContent(loginBody, Encoding.UTF8, "application/json");
            using var loginResponse = await _httpClient.PostAsync("/api/auth/bot-login", loginContent, cancellationToken);
            if (!loginResponse.IsSuccessStatusCode)
            {
                var failedBody = await loginResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Bot login failed: {StatusCode} {Body}", loginResponse.StatusCode, failedBody);
                return;
            }

            var responseJson = await loginResponse.Content.ReadAsStringAsync(cancellationToken);
            using var responseDocument = JsonDocument.Parse(responseJson);

            string? newToken = null;
            if (responseDocument.RootElement.TryGetProperty("accessToken", out var tokenProperty))
            {
                newToken = tokenProperty.GetString();
            }
            else if (responseDocument.RootElement.TryGetProperty("token", out var fallbackTokenProperty))
            {
                newToken = fallbackTokenProperty.GetString();
            }

            DateTime newExpiresAt;
            if (responseDocument.RootElement.TryGetProperty("expiresAt", out var expiresAtProperty)
                && expiresAtProperty.ValueKind == JsonValueKind.String
                && DateTime.TryParse(expiresAtProperty.GetString(), out var parsedExpiresAt))
            {
                newExpiresAt = parsedExpiresAt.ToUniversalTime();
            }
            else
            {
                newExpiresAt = DateTime.UtcNow.AddHours(1);
            }

            if (!string.IsNullOrWhiteSpace(newToken))
            {
                // Volatile.Write — гарантирует, что другие потоки увидят свежие значения.
                Volatile.Write(ref _accessToken, newToken);
                Volatile.Write(ref _accessTokenExpiresAtTicks, newExpiresAt.Ticks);
                ApplyAuthorizationHeader(newToken);
            }
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private void ApplyAuthorizationHeader(string token)
    {
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    /// <summary>
    /// Проверяет код привязки
    /// </summary>
    public async Task<VerifyConnectionCodeResponse> VerifyConnectionCodeAsync(
        string connectionCode, 
        long telegramUserId, 
        string? telegramUsername = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Проверка кода привязки {Code} для Telegram {UserId}", 
                connectionCode, telegramUserId);

            await EnsureAuthenticatedAsync(cancellationToken);

            var request = new VerifyConnectionCodeRequest
            {
                ConnectionCode = connectionCode,
                TelegramUserId = telegramUserId,
                TelegramUsername = telegramUsername
            };

            var json = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/parent-link/verify", content, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<VerifyConnectionCodeResponse>(responseJson, JsonSerializerOptions.Web);
                
                if (result != null)
                {
                    _logger.LogInformation("Код проверен: {IsValid}", result.IsValid);
                    return result;
                }
            }

            _logger.LogWarning("Ошибка API: {StatusCode} - {Response}", response.StatusCode, responseJson);
            return new VerifyConnectionCodeResponse
            {
                Success = false,
                IsValid = false,
                ErrorMessage = $"Ошибка API: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке кода привязки");
            return new VerifyConnectionCodeResponse
            {
                Success = false,
                IsValid = false,
                ErrorMessage = "Ошибка соединения с сервером"
            };
        }
    }

    /// <summary>
    /// Получает содержимое рюкзака ребёнка
    /// </summary>
    public async Task<BackpackResponse?> GetBackpackAsync(Guid childId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Получение рюкзака для ребёнка {ChildId}", childId);

            await EnsureAuthenticatedAsync(cancellationToken);
            var response = await _httpClient.GetAsync($"/api/backpack/{childId}", cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<BackpackResponse>(responseJson, JsonSerializerOptions.Web);
                _logger.LogInformation("Рюкзак получен: {ItemCount} перекусов", result?.TotalItems ?? 0);
                return result;
            }

            _logger.LogWarning("Ошибка получения рюкзака: {StatusCode} - {Response}", response.StatusCode, responseJson);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении рюкзака");
            return null;
        }
    }

    /// <summary>
    /// Добавляет новый перекус в рюкзак
    /// </summary>
    public async Task<BackpackItemResponse?> AddSnackAsync(
        Guid childId, 
        string snackName, 
        decimal breadUnits, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Добавление перекуса {SnackName} ({BreadUnits} ХЕ) для ребёнка {ChildId}", 
                snackName, breadUnits, childId);

            var request = new CreateBackpackItemRequest
            {
                ChildId = childId,
                SnackName = snackName,
                BreadUnits = breadUnits,
                AddedBy = "parent"
            };

            var json = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await EnsureAuthenticatedAsync(cancellationToken);
            var response = await _httpClient.PostAsync("/api/backpack", content, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<BackpackItemResponse>(responseJson, JsonSerializerOptions.Web);
                _logger.LogInformation("Перекус добавлен: {SnackName}", result?.SnackName);
                return result;
            }

            _logger.LogWarning("Ошибка добавления перекуса: {StatusCode} - {Response}", response.StatusCode, responseJson);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при добавлении перекуса");
            return null;
        }
    }

    /// <summary>
    /// Удаляет перекус из рюкзака
    /// </summary>
    public async Task<bool> RemoveSnackAsync(Guid backpackItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Удаление перекуса {ItemId}", backpackItemId);

            await EnsureAuthenticatedAsync(cancellationToken);
            var response = await _httpClient.DeleteAsync($"/api/backpack/{backpackItemId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Перекус удалён");
                return true;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Ошибка удаления перекуса: {StatusCode} - {Response}", response.StatusCode, responseJson);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении перекуса");
            return false;
        }
    }

    /// <summary>
    /// Получает статистику измерений за период
    /// </summary>
    public async Task<StatisticsResponse?> GetStatisticsAsync(
        Guid childId, 
        string period = "day", 
        DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Получение статистики для ребёнка {ChildId}, период {Period}", childId, period);

            var queryParams = new List<string> { $"period={period}" };
            if (date.HasValue)
            {
                queryParams.Add($"date={date.Value:yyyy-MM-dd}");
            }

            var queryString = string.Join("&", queryParams);
            var url = $"/api/measurements/{childId}/statistics?{queryString}";

            await EnsureAuthenticatedAsync(cancellationToken);
            var response = await _httpClient.GetAsync(url, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<StatisticsResponse>(responseJson, JsonSerializerOptions.Web);
                _logger.LogInformation("Статистика получена: {Count} измерений", result?.TotalMeasurements ?? 0);
                return result;
            }

            _logger.LogWarning("Ошибка получения статистики: {StatusCode} - {Response}", response.StatusCode, responseJson);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статистики");
            return null;
        }
    }

    /// <summary>
    /// Экспортирует статистику в PDF
    /// </summary>
    public async Task<byte[]?> ExportStatisticsToPdfAsync(
        Guid childId, 
        string period = "day", 
        bool detailed = false,
        DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Экспорт PDF для ребёнка {ChildId}, период {Period}, подробный: {Detailed}", 
                childId, period, detailed);

            var queryParams = new List<string> 
            { 
                $"period={period}",
                $"detailed={detailed.ToString().ToLower()}"
            };
            
            if (date.HasValue)
            {
                queryParams.Add($"date={date.Value:yyyy-MM-dd}");
            }

            var queryString = string.Join("&", queryParams);
            var url = $"/api/measurements/{childId}/export-pdf?{queryString}";

            await EnsureAuthenticatedAsync(cancellationToken);
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                _logger.LogInformation("PDF сгенерирован, размер: {Size} байт", pdfBytes.Length);
                return pdfBytes;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Ошибка генерации PDF: {StatusCode} - {Response}", response.StatusCode, responseJson);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при экспорте PDF");
            return null;
        }
    }

    /// <summary>
    /// Проверяет доступность API
    /// </summary>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);
            var response = await _httpClient.GetAsync("/api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке доступности API");
            return false;
        }
    }
}

/// <summary>
/// Запрос на проверку кода привязки
/// </summary>
public class VerifyConnectionCodeRequest
{
    public string ConnectionCode { get; set; } = string.Empty;
    public long TelegramUserId { get; set; }
    public string? TelegramUsername { get; set; }
}

/// <summary>
/// Ответ на проверку кода привязки
/// </summary>
public class VerifyConnectionCodeResponse
{
    public bool Success { get; set; }
    public bool IsValid { get; set; }
    public Guid? ChildId { get; set; }
    public Guid? LinkId { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Запрос на создание нового перекуса в рюкзаке
/// </summary>
public class CreateBackpackItemRequest
{
    public Guid ChildId { get; set; }
    public string SnackName { get; set; } = string.Empty;
    public decimal BreadUnits { get; set; }
    public string? AddedBy { get; set; } = "parent";
}

/// <summary>
/// Ответ с данными перекуса из рюкзака
/// </summary>
public class BackpackItemResponse
{
    public Guid BackpackItemId { get; set; }
    public Guid ChildId { get; set; }
    public string SnackName { get; set; } = string.Empty;
    public decimal BreadUnits { get; set; }
    public string? AddedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Ответ с полным содержимым рюкзака ребёнка
/// </summary>
public class BackpackResponse
{
    public Guid ChildId { get; set; }
    public List<BackpackItemResponse> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public decimal TotalBreadUnits { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Ответ со статистическими данными измерений
/// </summary>
public class StatisticsResponse
{
    public Guid ChildId { get; set; }
    public string Period { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalMeasurements { get; set; }
    public double AverageGlucose { get; set; }
    public double MinGlucose { get; set; }
    public double MaxGlucose { get; set; }
    public double StandardDeviation { get; set; }
    public double TimeInTargetRange { get; set; }
    public int HypoEpisodes { get; set; }
    public int HyperEpisodes { get; set; }
    public int CriticalEpisodes { get; set; }
    public List<MeasurementResponseBot> Measurements { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Данные измерения для бота
/// </summary>
public class MeasurementResponseBot
{
    public Guid MeasurementId { get; set; }
    public decimal GlucoseValue { get; set; }
    public DateTime MeasurementTime { get; set; }
    public string? Notes { get; set; }
    public string GlucoseStatus { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
}

