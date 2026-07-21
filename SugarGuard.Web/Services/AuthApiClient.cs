using SugarGuard.Shared.Dto;
using SugarGuard.Web.Models.Auth;
using SugarGuard.Web.Services.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace SugarGuard.Web.Services;

/// <summary>
/// Реализация AuthApiClient
/// </summary>
public sealed class AuthApiClient : IAuthApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthApiClient> _logger;

    // Маршруты API 
    private const string RouteLogin = "api/auth/login";
    private const string RouteRegister = "api/auth/register";
    private const string RouteVerifyEmail = "api/auth/verify-email";
    private const string RouteResendVerification = "api/auth/resend-verification";
    private const string RouteLogout = "api/auth/logout";
    private const string RouteForgotPassword = "api/auth/forgot-password";
    private const string RouteResetPassword = "api/auth/reset-password";
    private const string RouteOnboardingStatus = "api/onboarding/status";
    private const string RouteVerifyEmailCode = "api/verification/verify-email";
    private const string RouteResendEmailCode = "api/verification/send-email";
    private const string RouteOnboardingChild = "api/onboarding/child";
    private const string RouteOnboardingComplete = "api/onboarding/complete";
    private const string RouteOnboardingSkip = "api/onboarding/skip";

    /// <summary>
    /// Инициализирует зависимости через DI
    /// </summary>
    public AuthApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<AuthApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // Фабрика клиента 
    /// <summary>
    /// Создаёт анонимный HTTP-клиент
    /// </summary>
    private HttpClient CreateAnonymousClient()
        => _httpClientFactory.CreateClient("SugarGuardApi");

    /// <summary>
    /// Создаёт авторизованный HTTP-клиент
    /// </summary>
    private HttpClient CreateAuthenticatedClient()
        => _httpClientFactory.CreateClient("SugarGuardApi");

    /// <inheritdoc/>
    public async Task<LoginResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateAnonymousClient();
            var response = await client.PostAsJsonAsync(
                RouteLogin,
                new { email, password },
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = TryExtractError(body);
                _logger.LogWarning(
                    "LoginAsync: неуспешный ответ. Status={Status} Body={Body}",
                    response.StatusCode, body);
                return LoginResult.Fail(error ?? $"Ошибка авторизации: {email}.");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Поддержка обоих вариантов ответа: accessToken / token
            var accessToken = root.TryGetProperty("accessToken", out var atProp)
                ? atProp.GetString()
                : root.TryGetProperty("token", out var tProp)
                    ? tProp.GetString()
                    : null;

            var refreshToken = root.TryGetProperty("refreshToken", out var rtProp)
                ? rtProp.GetString()
                : null;

            DateTime? expiresAt = root.TryGetProperty("expiresAt", out var expProp)
                && expProp.ValueKind == JsonValueKind.String
                && DateTime.TryParse(expProp.GetString(), out var parsedExp)
                    ? parsedExp.ToUniversalTime()
                    : null;

            var role = root.TryGetProperty("role", out var roleProp)
                ? roleProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("LoginAsync: access-токен отсутствует в ответе.");
                return LoginResult.Fail("Сервер не вернул токен доступа.");
            }

            return LoginResult.Ok(accessToken, refreshToken, expiresAt, role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoginAsync: необработанное исключение.");
            return LoginResult.Fail("Не удалось выполнить вход. Попробуйте позже.");
        }
    }

    /// <inheritdoc/>
    public async Task<RegisterResult> RegisterAsync(
        string email,
        string password,
        string role = "Parent",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateAnonymousClient();
            var response = await client.PostAsJsonAsync(
                RouteRegister,
                new { email, password, role },
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = TryExtractError(body);
                _logger.LogWarning(
                    "RegisterAsync: неуспешный ответ. Status={Status} Body={Body}",
                    response.StatusCode, body);
                return RegisterResult.Fail(error ?? "Ошибка регистрации. Попробуйте позже.");
            }

            var requiresEmailVerification = true;
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("requiresEmailVerification", out var requiresProp)
                    && requiresProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    requiresEmailVerification = requiresProp.GetBoolean();
                }
            }

            return RegisterResult.Ok(email, requiresEmailVerification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RegisterAsync: необработанное исключение.");
            return RegisterResult.Fail("Не удалось зарегистрироваться. Попробуйте позже.");
        }
    }

    /// <inheritdoc/>
    public async Task<VerifyEmailResult> VerifyEmailAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateAnonymousClient();
            var response = await client.PostAsJsonAsync(
                RouteVerifyEmail,
                new { email, code },
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = TryExtractError(body);
                _logger.LogWarning(
                    "VerifyEmailAsync: неуспешный ответ. Status={Status} Body={Body}",
                    response.StatusCode, body);
                return VerifyEmailResult.Fail(error ?? "Неверный или просроченный код.");
            }

            return VerifyEmailResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyEmailAsync: необработанное исключение.");
            return VerifyEmailResult.Fail("Не удалось подтвердить email. Попробуйте позже.");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SendEmailVerificationCodeAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateAnonymousClient();
            var response = await client.PostAsJsonAsync(
                RouteResendVerification,
                new { email },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "SendEmailVerificationCodeAsync: неуспешный ответ. Status={Status} Body={Body}",
                    response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendEmailVerificationCodeAsync: необработанное исключение.");
            return false;
        }
    }

    // Logout 
    /// <inheritdoc/>
    public async Task LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token, cancellationToken);

            var client = _httpClientFactory.CreateClient("SugarGuardApi");
            await client.PostAsJsonAsync(
                RouteLogout,
                new { refreshToken },
                linkedCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LogoutAsync: ошибка при завершении сессии на сервере.");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SendPasswordResetCodeAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateAnonymousClient();
            var response = await client.PostAsJsonAsync(
                RouteForgotPassword,
                new { email = email.Trim().ToLowerInvariant() },
                cancellationToken);

            return response.IsSuccessStatusCode
                || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SendPasswordResetCodeAsync: необработанное исключение. Email={Email}", email);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<ResetPasswordResult> ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateAnonymousClient();
            var response = await client.PostAsJsonAsync(
                RouteResetPassword,
                new
                {
                    email = email.Trim().ToLowerInvariant(),
                    code,
                    newPassword
                },
                cancellationToken);

            if (response.IsSuccessStatusCode)
                return ResetPasswordResult.Ok("Пароль успешно изменён.");

            var body = await response.Content
                .ReadFromJsonAsync<AuthApiErrorBody>(cancellationToken: cancellationToken);

            return ResetPasswordResult.Fail(
                body?.Message ?? body?.Detail ?? body?.Error ?? "Не удалось сбросить пароль.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ResetPasswordAsync: необработанное исключение. Email={Email}", email);
            return ResetPasswordResult.Fail("Не удалось сбросить пароль. Попробуйте позже.");
        }
    }

    /// <inheritdoc/>
    public async Task<OnboardingStatusResponse> GetOnboardingStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync(RouteOnboardingStatus, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await BuildApiExceptionAsync(response, cancellationToken);

        return await response.Content
            .ReadFromJsonAsync<OnboardingStatusResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                $"API вернул пустое тело для {RouteOnboardingStatus}.");
    }

    /// <inheritdoc/>
    public async Task<VerifyEmailResponse> VerifyEmailCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var client = CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync(
            RouteVerifyEmailCode,
            new { code },
            cancellationToken);

        if (response.IsSuccessStatusCode)
            return await response.Content
                .ReadFromJsonAsync<VerifyEmailResponse>(cancellationToken: cancellationToken)
                ?? new VerifyEmailResponse { IsValid = false, ErrorMessage = "Пустой ответ сервера." };

        return new VerifyEmailResponse
        {
            IsValid = false,
            ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.BadRequest
                ? "Неверный или просроченный код подтверждения."
                : "Ошибка сервера. Попробуйте позже."
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ResendEmailVerificationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateAuthenticatedClient();
            var response = await client.PostAsync(
                RouteResendEmailCode,
                content: null,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ResendEmailVerificationAsync: необработанное исключение.");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<CreateChildOnboardingResponse> CreateChildOnboardingAsync(
        CreateChildOnboardingRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync(
            RouteOnboardingChild,
            request,
            cancellationToken);

        if (response.IsSuccessStatusCode)
            return await response.Content
                .ReadFromJsonAsync<CreateChildOnboardingResponse>(cancellationToken: cancellationToken)
                ?? new CreateChildOnboardingResponse
                {
                    Success = false,
                    ErrorMessage = "Сервер вернул пустой ответ."
                };

        var body = await response.Content
            .ReadFromJsonAsync<AuthApiErrorBody>(cancellationToken: cancellationToken);

        return new CreateChildOnboardingResponse
        {
            Success = false,
            ErrorMessage = body?.Message ?? body?.Detail ?? "Не удалось создать профиль ребёнка."
        };
    }

    /// <inheritdoc/>
    public async Task<OnboardingStatusResponse> CompleteOnboardingStepAsync(
        int step,
        CancellationToken cancellationToken = default)
    {
        var client = CreateAuthenticatedClient();
        var route = $"api/onboarding/steps/{step}/complete";
        var response = await client.PostAsync(route, content: null, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await BuildApiExceptionAsync(response, cancellationToken);

        return await response.Content
            .ReadFromJsonAsync<OnboardingStatusResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"API вернул пустое тело для {route}.");
    }

    /// <inheritdoc/>
    public async Task<CompleteOnboardingResponse> CompleteOnboardingAsync(
        CompleteOnboardingRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync(
            RouteOnboardingComplete,
            request,
            cancellationToken);

        if (response.IsSuccessStatusCode)
            return await response.Content
                .ReadFromJsonAsync<CompleteOnboardingResponse>(cancellationToken: cancellationToken)
                ?? new CompleteOnboardingResponse
                {
                    Success = false,
                    ErrorMessage = "Сервер вернул пустой ответ."
                };

        var body = await response.Content
            .ReadFromJsonAsync<AuthApiErrorBody>(cancellationToken: cancellationToken);

        return new CompleteOnboardingResponse
        {
            Success = false,
            ErrorMessage = body?.Message ?? body?.Detail ?? "Не удалось завершить онбординг."
        };
    }

    /// <inheritdoc/>
    public async Task SkipOnboardingAsync(CancellationToken cancellationToken = default)
    {
        var client = CreateAuthenticatedClient();
        var response = await client.PostAsync(
            RouteOnboardingSkip,
            content: null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "SkipOnboardingAsync: API вернул ошибку. StatusCode={StatusCode}",
                response.StatusCode);
        }
    }

    /// <inheritdoc/>
    public async Task<Guid> GetCurrentChildIdAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await GetOnboardingStatusAsync(cancellationToken);
            return status.ChildId ?? Guid.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetCurrentChildIdAsync: не удалось получить ChildId.");
            return Guid.Empty;
        }
    }

    /// <inheritdoc/>
    public async Task UpdateDiabetesSettingsAsync(
        Guid childId,
        UpdateDiabetesSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateAuthenticatedClient();
            var response = await client.PutAsJsonAsync(
                $"api/dashboard/{childId}/settings",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "UpdateDiabetesSettingsAsync: API вернул ошибку. ChildId={ChildId} StatusCode={StatusCode}",
                    childId, response.StatusCode);
                throw await BuildApiExceptionAsync(response, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex,
                "UpdateDiabetesSettingsAsync: необработанное исключение. ChildId={ChildId}", childId);
            throw;
        }
    }

    // Приватные вспомогательные методы 
    /// <summary>
    /// Пытается извлечь читаемое сообщение об ошибке из JSON-тела ответа
    /// </summary>
    private static string? TryExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            foreach (var field in new[] { "errorMessage", "message", "detail", "title" })
            {
                if (root.TryGetProperty(field, out var prop)
                    && prop.ValueKind == JsonValueKind.String)
                {
                    var val = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                        return val;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    /// <summary>
    /// Строит HttpRequestException с читаемым сообщением из тела ответа
    /// </summary>
    private static async Task<Exception> BuildApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string? message = null;

        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            message = TryExtractError(body);
        }
        catch
        {

        }

        return new HttpRequestException(
            message ?? $"API вернул ошибку: {(int)response.StatusCode} {response.ReasonPhrase}",
            inner: null,
            statusCode: response.StatusCode);
    }

    // Внутренние DTO 
    /// <summary>
    /// Внутренний DTO для десериализации ProblemDetails-ответов API
    /// </summary>
    private sealed record AuthApiErrorBody(
        string? Message,
        string? Detail,
        string? Error);
}
