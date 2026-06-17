// Реальный HTTP-клиент к SugarGuard API с JWT и автоматическим refresh при 401.
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;
using SugarGuard.Shared.Dto;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Реальный API-клиент: HTTP-запросы к SugarGuard API с Bearer-токеном.
/// При получении 401 автоматически обновляет access-токен и повторяет запрос.
/// </summary>
public class RealApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ISecureStorageService _secureStorage;
    private readonly ILogger<RealApiClient> _logger;

    // Lazy<T> разрывает циклическую зависимость:
    // AuthenticationService → RealApiClient → AuthenticationService
    private readonly Lazy<IAuthenticationService> _authService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public RealApiClient(
        HttpClient httpClient,
        ISecureStorageService secureStorage,
        ILogger<RealApiClient> logger,
        Lazy<IAuthenticationService> authService)
    {
        _httpClient = httpClient;
        _secureStorage = secureStorage;
        _logger = logger;
        _authService = authService;
    }

    // ─────────────────────────────────────────────────────────────
    // Инфраструктура: заголовок + retry при 401
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Выполняет HTTP-запрос. При 401 пробует обновить токен один раз и повторяет.
    /// JWT-заголовок добавляется автоматически через <see cref="Infrastructure.Handlers.JwtAuthorizationHandler" />.
    /// <para>
    /// Использование:
    /// <code>
    /// using var res = await SendWithRetryAsync(() =>
    ///     new HttpRequestMessage(HttpMethod.Post, "api/measurements")
    ///     { Content = JsonContent.Create(body, options: JsonOptions) });
    /// </code>
    /// </para>
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> buildRequest,
        CancellationToken ct = default)
    {
        var res = await _httpClient.SendAsync(buildRequest(), ct);

        if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogInformation("401 получен — пробуем обновить токен...");
            var refreshed = await _authService.Value.RefreshTokenAsync();

            if (refreshed)
            {
                res.Dispose();
                res = await _httpClient.SendAsync(buildRequest(), ct);
                _logger.LogInformation("Повторный запрос после refresh: {Status}", res.StatusCode);
            }
            else
            {
                _logger.LogWarning("Refresh не удался — сессия истекла");
            }
        }

        return res;
    }

    // ─────────────────────────────────────────────────────────────
    // Auth — без retry (сами являются частью auth-flow)
    // ─────────────────────────────────────────────────────────────

    public async Task<LoginResponse> LoginAsync(string email, string password)
    {
        var body = new { email, password };
        using var res = await _httpClient.PostAsJsonAsync("api/auth/login", body, JsonOptions);

        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            _logger.LogWarning("Login failed: {Status} {Body}", res.StatusCode, err);
            return new LoginResponse { Success = false, ErrorMessage = "Неверный email или пароль" };
        }

        var data = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = data.TryGetProperty("accessToken", out var at) ? at.GetString()
                        : data.TryGetProperty("token", out var t) ? t.GetString()
                        : null;

        return new LoginResponse
        {
            Success = data.TryGetProperty("success", out var s) && s.GetBoolean(),
            AccessToken = accessToken,
            Token = accessToken,
            RefreshToken = data.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null,
            Message = data.TryGetProperty("message", out var m) ? m.GetString() : null
        };
    }

    public async Task<RegistrationResponse> RegisterAsync(RegistrationRequest request)
    {
        // Регистрация — публичный эндпоинт, без auth-заголовка
        using var res = await _httpClient.PostAsJsonAsync("api/auth/register", request, JsonOptions);
        if (!res.IsSuccessStatusCode)
        {
            var message = await ReadApiErrorMessageAsync(res);
            _logger.LogWarning("Register failed: {Status} {Message}", res.StatusCode, message);
            return new RegistrationResponse
            {
                Success = false,
                Message = message,
                ErrorMessage = message
            };
        }

        var data = await res.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);
        return data ?? new RegistrationResponse { Success = false };
    }

    private static async Task<string> ReadApiErrorMessageAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return $"Сервер вернул ошибку {(int)response.StatusCode}.";
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                {
                    if (field.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var item in field.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(item.GetString()))
                        {
                            return item.GetString()!;
                        }
                    }
                }
            }

            foreach (var propertyName in new[] { "message", "detail", "errorMessage", "error", "title" })
            {
                if (root.TryGetProperty(propertyName, out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    var text = value.GetString()!;
                    if (!string.Equals(text, "One or more validation errors occurred.", StringComparison.Ordinal))
                    {
                        return text;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Older endpoints may still return plain text.
        }

        return raw.Length <= 240 ? raw : raw[..240];
    }

    public async Task<VerifyCodeResponse> VerifyEmailAsync(string email, string code)
    {
        try
        {
            var body = new { email, code };
            using var res = await _httpClient.PostAsJsonAsync("api/auth/verify-email", body, JsonOptions);

            if (!res.IsSuccessStatusCode)
            {
                var message = await ReadApiErrorMessageAsync(res);
                _logger.LogWarning("VerifyEmail failed: {Status} {Message}", res.StatusCode, message);
                return new VerifyCodeResponse { IsValid = false, Message = message, ErrorMessage = message };
            }

            var data = await res.Content.ReadFromJsonAsync<VerifyCodeResponse>(JsonOptions);
            return data ?? new VerifyCodeResponse { IsValid = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при подтверждении email");
            return new VerifyCodeResponse { IsValid = false, Message = "Ошибка сети.", ErrorMessage = "Ошибка сети." };
        }
    }

    public async Task<bool> SendEmailVerificationCodeAsync(string email)
    {
        try
        {
            var body = new { email };
            using var res = await _httpClient.PostAsJsonAsync("api/auth/resend-verification", body, JsonOptions);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке кода подтверждения");
            return false;
        }
    }

    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken)
    {
        // Не используем SendWithRetryAsync — это и есть механизм обновления
        // JWT-заголовок добавляется автоматически через JwtAuthorizationHandler
        var res = await _httpClient.PostAsJsonAsync("api/auth/refresh", new { refreshToken }, JsonOptions);
        if (!res.IsSuccessStatusCode)
            return new LoginResponse { Success = false };

        var data = await res.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        return data ?? new LoginResponse { Success = false };
    }

    public async Task LogoutAsync(string refreshToken)
    {
        try
        {
            using var res = await _httpClient.PostAsJsonAsync(
                "api/auth/logout", new { refreshToken }, JsonOptions);

            if (res.IsSuccessStatusCode)
                _logger.LogInformation("Refresh-токен отозван на сервере");
            else
                _logger.LogWarning("Сервер вернул {StatusCode} при логауте", res.StatusCode);
        }
        catch (Exception ex)
        {
            // Проглатываем — локальный логаут не должен зависеть от сети
            _logger.LogWarning("Не удалось отозвать токен: {Message}", ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Измерения
    // ─────────────────────────────────────────────────────────────

    public async Task<CreateChildOnboardingResponse> CreateChildOnboardingAsync(CreateChildOnboardingRequest request)
    {
        try
        {
            using var res = await SendWithRetryAsync(() =>
                new HttpRequestMessage(HttpMethod.Post, "api/onboarding/child")
                {
                    Content = JsonContent.Create(request, options: JsonOptions)
                });

            if (!res.IsSuccessStatusCode)
            {
                var message = await ReadApiErrorMessageAsync(res);
                _logger.LogWarning("CreateChildOnboarding failed: {Status} {Message}", res.StatusCode, message);
                return new CreateChildOnboardingResponse
                {
                    Success = false,
                    ErrorMessage = message
                };
            }

            return await res.Content.ReadFromJsonAsync<CreateChildOnboardingResponse>(JsonOptions)
                ?? new CreateChildOnboardingResponse
                {
                    Success = false,
                    ErrorMessage = "Сервер вернул пустой ответ."
                };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания профиля ребенка");
            return new CreateChildOnboardingResponse
            {
                Success = false,
                ErrorMessage = "Не удалось создать профиль ребенка. Проверьте подключение."
            };
        }
    }

    public async Task<MeasurementResponse> SendMeasurementAsync(SendMeasurementRequest request)
    {
        var apiBody = new
        {
            childId = Guid.TryParse(request.ChildId, out var cid) ? cid : Guid.Empty,
            glucoseValue = request.GlucoseValue,
            measurementTime = request.MeasurementTime,
            childState = request.ChildState,
            notes = request.Notes,
            dataSource = request.DataSource ?? "mobile_app"
        };

        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Post, "api/measurements")
            { Content = JsonContent.Create(apiBody, options: JsonOptions) });

        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            _logger.LogWarning("SendMeasurement failed: {Status} {Body}", res.StatusCode, err);
            return new MeasurementResponse { Success = false, ErrorMessage = err };
        }

        var raw = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var response = new MeasurementResponse
        {
            Success = true,
            MeasurementId = raw.TryGetProperty("measurementId", out var mid) ? mid.GetString() : null,
            IsCritical = raw.TryGetProperty("isCritical", out var ic) && ic.GetBoolean(),
            ErrorMessage = raw.TryGetProperty("errorMessage", out var em) ? em.GetString() : null
        };

        if (raw.TryGetProperty("recommendation", out var rec) && rec.ValueKind == JsonValueKind.Object)
            response.Recommendation = JsonSerializer.Deserialize<RecommendationResponse>(rec.GetRawText(), JsonOptions);

        return response;
    }

    public async Task<SyncResponse> SyncMeasurementsAsync(SyncRequest request)
    {
        var list = request.Measurements.Select(m => new
        {
            childId = Guid.TryParse(m.ChildId, out var cid) ? cid : Guid.Empty,
            glucoseValue = m.GlucoseValue,
            measurementTime = m.MeasurementTime,
            childState = m.ChildState,
            notes = m.Notes,
            dataSource = m.DataSource ?? "mobile_app"
        }).ToList();

        var body = new { measurements = list };

        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Post, "api/measurements/sync")
            { Content = JsonContent.Create(body, options: JsonOptions) });

        if (!res.IsSuccessStatusCode)
            return new SyncResponse { Success = false, SuccessCount = 0, ErrorCount = request.Measurements.Count };

        var data = await res.Content.ReadFromJsonAsync<SyncResponse>(JsonOptions);
        return data ?? new SyncResponse { Success = true, SuccessCount = request.Measurements.Count, ErrorCount = 0 };
    }

    public async Task<MeasurementResponse> GetLatestMeasurementAsync(string childId)
    {
        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Get, $"api/measurements/{childId}/latest"));

        if (!res.IsSuccessStatusCode)
            return new MeasurementResponse { Success = false, ErrorMessage = res.ReasonPhrase };

        var raw = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return ParseMeasurementResponse(raw);
    }

    public async Task<MeasurementResponse> GetMeasurementByIdAsync(string measurementId)
    {
        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Get, $"api/measurements/by-id/{measurementId}"));

        if (!res.IsSuccessStatusCode)
            return new MeasurementResponse { Success = false, ErrorMessage = res.ReasonPhrase };

        var raw = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return ParseMeasurementResponse(raw);
    }

    private static MeasurementResponse ParseMeasurementResponse(JsonElement raw)
    {
        return new MeasurementResponse
        {
            Success = true,
            MeasurementId = raw.TryGetProperty("measurementId", out var mid) ? mid.GetString() : null,
            IsCritical = raw.TryGetProperty("isCritical", out var ic) && ic.GetBoolean(),
            ServerModifiedAt = raw.TryGetProperty("serverModifiedAt", out var sma) && sma.ValueKind == JsonValueKind.String
                ? DateTime.Parse(sma.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : null,
            ServerVersion = raw.TryGetProperty("serverVersion", out var sv) ? sv.GetRawText() : null
        };
    }

    // ─────────────────────────────────────────────────────────────
    // ИИ-рекомендации
    // ─────────────────────────────────────────────────────────────

    public async Task<RecommendationResponse?> GetRecommendationAsync(RecommendationRequest request)
    {
        var apiBody = new
        {
            childId = Guid.TryParse(request.ChildId, out var cid) ? cid : Guid.Empty,
            glucoseValue = (decimal)request.CurrentGlucose,
            availableSnacks = request.AvailableSnacks,
            forceNew = false
        };

        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Post, "api/recommendations")
            { Content = JsonContent.Create(apiBody, options: JsonOptions) });

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("GetRecommendation failed: {Status}", res.StatusCode);
            return null;
        }

        var raw = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return new RecommendationResponse
        {
            RecommendationId = raw.TryGetProperty("recommendationId", out var rid) ? rid.GetString() ?? "" : "",
            RecommendationText = raw.TryGetProperty("recommendationText", out var rt) ? rt.GetString() ?? "" : "",
            Text = raw.TryGetProperty("recommendationText", out var tx) ? tx.GetString() : null,
            Urgency = raw.TryGetProperty("urgency", out var u) ? u.GetString() ?? "Normal" : "Normal",
            Success = true,
            ModelUsed = raw.TryGetProperty("modelUsed", out var mu) ? mu.GetString() : null,
            Model = raw.TryGetProperty("modelUsed", out var mo) ? mo.GetString() : null,
            LatencyMs = raw.TryGetProperty("latencyMs", out var lm) ? lm.GetInt64() : 0,
            GlucoseValueAtRequest = raw.TryGetProperty("glucoseValueAtRequest", out var gv) ? gv.GetDouble() : request.CurrentGlucose,
            IsFromCache = raw.TryGetProperty("isFromCache", out var fc) && fc.GetBoolean(),
            CreatedAt = raw.TryGetProperty("createdAt", out var ca) && ca.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow
        };
    }

    // ─────────────────────────────────────────────────────────────
    // Подключение родителя / Telegram
    // ─────────────────────────────────────────────────────────────

    public async Task<TelegramConnectResponse> GenerateTelegramCodeAsync(string childId)
    {
        var body = new { childId = Guid.TryParse(childId, out var cid) ? cid : Guid.Empty, codeHash = "" };

        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Post, "api/parent-link/code")
            { Content = JsonContent.Create(body, options: JsonOptions) });

        if (!res.IsSuccessStatusCode)
            return new TelegramConnectResponse { Success = false };

        var data = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return new TelegramConnectResponse
        {
            Success = data.TryGetProperty("success", out var s) && s.GetBoolean(),
            ConnectionCodeId = data.TryGetProperty("codeId", out var c) ? c.GetString() : null,
            ExpiresIn = data.TryGetProperty("expiresAt", out _) ? 600 : 0
        };
    }

    public Task<bool> VerifyTelegramConnectionAsync(VerifyTelegramCodeRequest request)
        => Task.FromResult(false);

    public async Task<SaveConnectionCodeResponse> SaveConnectionCodeAsync(SaveConnectionCodeRequest request)
    {
        // SEC-2: шлём сырой код — сервер хеширует HMAC-SHA256 сам.
        var body = new
        {
            childId = Guid.TryParse(request.ChildId, out var cid) ? cid : Guid.Empty,
            code = request.Code
        };

        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Post, "api/parent-link/code")
            { Content = JsonContent.Create(body, options: JsonOptions) });

        var data = await res.Content.ReadFromJsonAsync<SaveConnectionCodeResponse>(JsonOptions);
        return data ?? new SaveConnectionCodeResponse { Success = false };
    }

    public async Task<VerifyConnectionCodeResponse> VerifyConnectionCodeAsync(VerifyConnectionCodeRequest request)
    {
        // Верификация кода — публичный эндпоинт (родитель ещё не авторизован)
        using var res = await _httpClient.PostAsJsonAsync("api/parent-link/verify", request, JsonOptions);
        var data = await res.Content.ReadFromJsonAsync<VerifyConnectionCodeResponse>(JsonOptions);
        return data ?? new VerifyConnectionCodeResponse { Success = false, IsValid = false };
    }

    // ─────────────────────────────────────────────────────────────
    // Рюкзак
    // ─────────────────────────────────────────────────────────────

    public async Task<bool> AddSnackAsync(AddSnackRequest request)
    {
        var body = new
        {
            childId = Guid.TryParse(request.ChildId, out var cid) ? cid : Guid.Empty,
            snackName = request.SnackName,
            breadUnits = request.Carbs,
            addedBy = request.AddedBy ?? "child"
        };

        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Post, "api/backpack")
            { Content = JsonContent.Create(body, options: JsonOptions) });

        return res.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveSnackAsync(RemoveSnackRequest request)
    {
        var itemId = Guid.TryParse(request.SnackId, out var g) ? g : Guid.Empty;

        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Delete, $"api/backpack/{itemId}"));

        return res.IsSuccessStatusCode;
    }

    public async Task<List<string>> GetBackpackAsync(string childId)
    {
        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Get, $"api/backpack/{childId}"));

        if (!res.IsSuccessStatusCode)
            return [];

        var data = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        if (data.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in items.EnumerateArray())
                if (item.TryGetProperty("snackName", out var sn))
                    list.Add(sn.GetString() ?? "");
            return list;
        }

        return [];
    }

    // ─────────────────────────────────────────────────────────────
    // Уведомления
    // ─────────────────────────────────────────────────────────────

    public async Task<bool> SendMeasurementNotificationAsync(MeasurementNotificationRequest request)
    {
        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Post, "api/notifications/measurement")
            { Content = JsonContent.Create(request, options: JsonOptions) });
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> SendSnackConsumedNotificationAsync(SnackConsumedNotificationRequest request)
    {
        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Post, "api/notifications/snack-consumed")
            { Content = JsonContent.Create(request, options: JsonOptions) });
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> SendCriticalAlertAsync(CriticalAlertRequest request)
    {
        var body = new
        {
            childId = request.ChildId,
            criticalGlucose = request.GlucoseValue,
            measurementTime = request.MeasurementTime,
            latitude = request.Latitude,
            longitude = request.Longitude,
            address = request.Address
        };

        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Post, "api/notifications/critical-alert")
            { Content = JsonContent.Create(body, options: JsonOptions) });

        return res.IsSuccessStatusCode;
    }

    public Task<bool> SendMissedMeasurementNotificationAsync(MissedMeasurementNotificationRequest request)
        => Task.FromResult(false);

    // ─────────────────────────────────────────────────────────────
    // Экспорт / утилиты
    // ─────────────────────────────────────────────────────────────

    public async Task<byte[]> ExportStatisticsToPdfAsync(string childId, string period = "day", bool detailed = false)
    {
        var url = $"api/measurements/{childId}/export-pdf?period={Uri.EscapeDataString(period)}&detailed={detailed}";

        using var res = await SendWithRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Get, url));

        return res.IsSuccessStatusCode
            ? await res.Content.ReadAsByteArrayAsync()
            : Array.Empty<byte>();
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            // Health check — публичный эндпоинт, без авторизации
            using var res = await _httpClient.GetAsync("api/health");
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Фото ребёнка (TODO-3)
    // ─────────────────────────────────────────────────────────────

    public async Task<string?> UploadChildPhotoAsync(
        string childId,
        Stream photoStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        try
        {
            using var multipart = new MultipartFormDataContent();
            var streamContent = new StreamContent(photoStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            multipart.Add(streamContent, "photo", fileName);

            using var res = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Post, $"api/children/{childId}/photo")
                { Content = multipart }, ct);

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("UploadChildPhoto failed: {Status} {Body}", res.StatusCode, err);
                return null;
            }

            var raw = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            return raw.TryGetProperty("photoUrl", out var pu) ? pu.GetString() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "UploadChildPhoto error");
            return null;
        }
    }

    public async Task<bool> DeleteChildPhotoAsync(string childId, CancellationToken ct = default)
    {
        try
        {
            using var res = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Delete, $"api/children/{childId}/photo"), ct);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "DeleteChildPhoto error");
            return false;
        }
    }
}
