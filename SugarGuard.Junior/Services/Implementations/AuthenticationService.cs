// Реализация сервиса аутентификации для MAUI-приложения SugarGuard Junior.
// Управляет жизненным циклом сессии: логин → refresh → логаут.
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

public class AuthenticationService(
    ILogger<AuthenticationService> logger,
    IApiClient apiClient,
    ISecureStorageService secureStorage,
    IStorageService storageService,
    IUserRepository userRepository,
    ICryptoService cryptoService) : IAuthenticationService
{
    // Ключи для хранилища
    private const string CurrentUserIdKey = "current_user_id";
    private const string CurrentUserKey = "current_user";
    private const string CurrentEmailKey = "current_email";
    private const string EmailVerifiedKey = "email_verified";
    private const string OfflineSessionVerifiedAtKey = "offline_session_verified_at_utc";
    private static readonly TimeSpan OfflineSessionLifetime = TimeSpan.FromDays(7);

    // ─────────────────────────────────────────────────────────────
    // Проверка состояния сессии
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Проверяет, авторизован ли текущий пользователь.
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var token = await secureStorage.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                if (await HasOfflineSessionAsync())
                {
                    logger.LogInformation("Токен отсутствует, но найдена локальная сессия. Разрешён офлайн-запуск.");
                    return true;
                }

                logger.LogInformation("Проверка аутентификации:  Не авторизован (токен отсутствует)");
                return false;
            }

            // Декодируем JWT и проверяем exp claim
            var exp = ParseJwtExpClaim(token);
            if (exp.HasValue && exp.Value > DateTime.UtcNow)
            {
                var weeklyRefreshResult = await TryRenewSessionWeeklyAsync();
                if (weeklyRefreshResult == SessionRefreshResult.Rejected)
                {
                    logger.LogWarning("Сервер отклонил refresh-токен при плановом обновлении сессии.");
                    await ClearLocalSessionAsync();
                    return false;
                }

                logger.LogInformation("Проверка аутентификации:  Авторизован (токен истекает {Exp})", exp.Value);
                return true;
            }

            // Токен истёк — пробуем refresh
            logger.LogInformation("Токен истёк, пробуем refresh...");
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet &&
                await HasOfflineSessionAsync())
            {
                logger.LogWarning("Сеть недоступна, используем сохранённую офлайн-сессию");
                return true;
            }

            var refreshResult = await TryRefreshSessionAsync();
            if (refreshResult == SessionRefreshResult.Success)
            {
                logger.LogInformation("Refresh успешен:  Авторизован");
                return true;
            }

            if (refreshResult == SessionRefreshResult.TemporarilyUnavailable && await HasOfflineSessionAsync())
            {
                logger.LogWarning("Сервер временно недоступен, используем подтверждённую офлайн-сессию");
                return true;
            }

            // Сервер явно отклонил refresh-токен либо срок офлайн-сессии истёк.
            logger.LogWarning("Сессия больше недействительна, выполняем logout");
            await ClearLocalSessionAsync();
            return false;
        }
        catch (Exception ex)
        {
            if (await HasOfflineSessionAsync())
            {
                logger.LogWarning(ex, "Ошибка проверки токена, используем сохранённую офлайн-сессию");
                return true;
            }

            logger.LogError(" Ошибка при проверке аутентификации: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Извлекает exp (expiration time) claim из JWT без валидации подписи.
    /// </summary>
    private static DateTime? ParseJwtExpClaim(string accessToken)
    {
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("exp", out var exp))
                return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()).UtcDateTime;
        }
        catch
        {
            // Невалидный JWT — не падаем
        }
        return null;
    }

    private async Task<bool> HasOfflineSessionAsync()
    {
        var userId = await storageService.GetAsync(CurrentUserIdKey);
        var childId = await storageService.GetAsync(SugarGuard.Junior.Utilities.Constants.StorageKeyCurrentChildId);

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(childId))
        {
            return false;
        }

        if (await userRepository.GetByIdAsync(userId) is null)
        {
            return false;
        }

        var verifiedAtText = await storageService.GetAsync(OfflineSessionVerifiedAtKey);
        if (DateTime.TryParse(
                verifiedAtText,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var verifiedAt))
        {
            return verifiedAt.ToUniversalTime() >= DateTime.UtcNow - OfflineSessionLifetime;
        }

        // Миграция существующих установок: наличие пары защищённых токенов и
        // локального профиля доказывает ранее успешный вход. Создаём первый
        // семидневный офлайн-допуск, не заставляя ребёнка входить в сеть после обновления.
        if (!string.IsNullOrWhiteSpace(await secureStorage.GetRefreshTokenAsync()))
        {
            await MarkSessionVerifiedAsync();
            logger.LogInformation("Создан офлайн-допуск для существующей локальной сессии.");
            return true;
        }

        return false;
    }

    private async Task<SessionRefreshResult> TryRenewSessionWeeklyAsync()
    {
        var verifiedAtText = await storageService.GetAsync(OfflineSessionVerifiedAtKey);
        var isDue = !DateTime.TryParse(
                        verifiedAtText,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var verifiedAt)
                    || verifiedAt.ToUniversalTime() <= DateTime.UtcNow - OfflineSessionLifetime;

        if (!isDue)
        {
            return SessionRefreshResult.Success;
        }

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            logger.LogInformation("Еженедельное обновление сессии отложено: нет интернета.");
            return SessionRefreshResult.TemporarilyUnavailable;
        }

        var refreshResult = await TryRefreshSessionAsync();
        if (refreshResult == SessionRefreshResult.Success)
        {
            logger.LogInformation("Выполнено еженедельное обновление ключа подключения к серверу.");
        }
        else
        {
            logger.LogWarning("Еженедельное обновление сессии не выполнено ({Result}); локальная сессия сохранена.", refreshResult);
        }

        return refreshResult;
    }

    private async Task<SessionRefreshResult> TryRefreshSessionAsync()
    {
        var refreshToken = await secureStorage.GetRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return SessionRefreshResult.Rejected;
        }

        try
        {
            var response = await apiClient.RefreshTokenAsync(refreshToken);
            if (response.Success && !string.IsNullOrWhiteSpace(response.AccessToken))
            {
                await secureStorage.SaveAuthTokenAsync(response.AccessToken, response.RefreshToken);
                await MarkSessionVerifiedAsync();
                return SessionRefreshResult.Success;
            }

            return response.IsRefreshTokenRejected
                ? SessionRefreshResult.Rejected
                : SessionRefreshResult.TemporarilyUnavailable;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Сервер недоступен при обновлении сессии.");
            return SessionRefreshResult.TemporarilyUnavailable;
        }
        catch (TaskCanceledException exception)
        {
            logger.LogWarning(exception, "Истёк таймаут при обновлении сессии.");
            return SessionRefreshResult.TemporarilyUnavailable;
        }
    }

    private Task MarkSessionVerifiedAsync() => storageService.SaveAsync(
        OfflineSessionVerifiedAtKey,
        DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

    private async Task ClearLocalSessionAsync()
    {
        secureStorage.ClearAuthTokens();
        await storageService.DeleteAsync(CurrentUserIdKey);
        await storageService.DeleteAsync(OfflineSessionVerifiedAtKey);
    }

    private enum SessionRefreshResult
    {
        Success,
        Rejected,
        TemporarilyUnavailable
    }

    /// <summary>
    /// Возвращает текущего авторизованного пользователя из локального репозитория.
    /// </summary>
    public async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            var userId = await storageService.GetAsync(CurrentUserIdKey);
            if (string.IsNullOrEmpty(userId))
            {
                logger.LogWarning("Текущий пользователь не найден");
                return null;
            }

            var user = await userRepository.GetByIdAsync(userId);
            if (user is not null)
                logger.LogInformation("Получен текущий пользователь: {UserId}", userId);

            return user;
        }
        catch (Exception ex)
        {
            logger.LogError(" Ошибка при получении текущего пользователя: {Message}", ex.Message);
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Регистрация
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Регистрирует нового пользователя через API.
    /// </summary>
    public async Task<User> RegisterAsync(string firstName, string lastName, string email,
        string phoneNumber, string password)
    {
        try
        {
            logger.LogInformation("Регистрация: {Email}", email);

            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("Имя не может быть пустым", nameof(firstName));

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email не может быть пустым", nameof(email));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Пароль не может быть пустым", nameof(password));

            var (isValidPassword, passwordErrors) = Utilities.Validators.IsValidPassword(password);
            if (!isValidPassword)
                throw new ArgumentException(
                    "Пароль не соответствует требованиям: " + string.Join(", ", passwordErrors));

            var registrationRequest = new RegistrationRequest
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PhoneNumber = phoneNumber,
                Password = password,
                Role = "ChildDevice"
            };

            var registrationResponse = await apiClient.RegisterAsync(registrationRequest);

            if (!registrationResponse.Success)
                throw new InvalidOperationException(registrationResponse.Message ?? "Ошибка регистрации");

            logger.LogInformation("Регистрация успешна: {Email}", email);

            var userId = registrationResponse.UserId
                ?? throw new InvalidOperationException("UserId не может быть null");

            var user = new User
            {
                UserId = userId,
                EncryptedFirstName = await cryptoService.EncryptAsync(firstName),
                EncryptedLastName = await cryptoService.EncryptAsync(lastName),
                EncryptedEmail = await cryptoService.EncryptAsync(email),
                EncryptedPhoneNumber = await cryptoService.EncryptAsync(phoneNumber),
                CreatedAt = DateTime.UtcNow
            };

            // Сохраняем в локальную БД
            await storageService.SaveAsync(CurrentUserIdKey, userId);
            await storageService.SaveAsync(CurrentEmailKey, email);
            await MarkSessionVerifiedAsync();

            try
            {
                var existingUser = await userRepository.GetByIdAsync(userId);
            if (existingUser is null)
            {
                await userRepository.AddAsync(user);
            }
            else
            {
                existingUser.EncryptedFirstName = user.EncryptedFirstName;
                existingUser.EncryptedLastName = user.EncryptedLastName;
                existingUser.EncryptedEmail = user.EncryptedEmail;
                existingUser.EncryptedPhoneNumber = user.EncryptedPhoneNumber;
                existingUser.IsEmailVerified = false;
                await userRepository.UpdateAsync(existingUser);
                user = existingUser;
            }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Registration succeeded on API, but local user cache was not saved. UserId={UserId}", userId);
            }
            // Сохраняем current_user_id в storage
            await storageService.SaveAsync(CurrentUserIdKey, userId);
            await storageService.SaveAsync(CurrentEmailKey, email);

            // Сохраняем токен если пришёл с регистрацией
            if (!string.IsNullOrEmpty(registrationResponse.Token))
            {
                await secureStorage.SaveAuthTokenAsync(registrationResponse.Token, null);
            }

            return user;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при регистрации: {Message}", ex.Message);
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Вход
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Выполняет вход через API, сохраняет access- и refresh-токены в SecureStorage.
    /// </summary>
    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            logger.LogInformation("Вход в аккаунт: {Email}", email);

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Email или пароль пустые");
                return false;
            }

            var loginResponse = await apiClient.LoginAsync(email, password);

            if (!loginResponse.Success)
            {
                logger.LogWarning("Ошибка входа: {Message}", loginResponse.Message);
                return false;
            }

            // Сохраняем оба токена в защищённое хранилище
            await secureStorage.SaveAuthTokenAsync(
                loginResponse.AccessToken
                    ?? throw new InvalidOperationException("AccessToken не может быть null"),
                loginResponse.RefreshToken);

            // ИСПРАВЛЕНО: UserId находится напрямую в LoginResponse, поля User нет
            var userId = ParseUserIdFromJwt(loginResponse.AccessToken!)
             ?? throw new InvalidOperationException("Не удалось получить UserId из токена");

            await storageService.SaveAsync(CurrentUserIdKey, userId);
            await storageService.SaveAsync(CurrentEmailKey, email);
            await MarkSessionVerifiedAsync();

            logger.LogInformation("Вход успешен: {Email} UserId={UserId}", email, userId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при входе: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Извлекает sub-клейм (UserId) из JWT без внешних библиотек.
    /// </summary>
    private static string? ParseUserIdFromJwt(string accessToken)
    {
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length != 3) return null;

            // Base64Url → Base64
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            // Дополняем до кратной 4 длины
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            // JWT sub = userId
            if (doc.RootElement.TryGetProperty("sub", out var sub))
                return sub.GetString();

            // Fallback: поле userId или nameid
            if (doc.RootElement.TryGetProperty("userId", out var uid))
                return uid.GetString();

            if (doc.RootElement.TryGetProperty(
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                out var nameId))
                return nameId.GetString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Обновление токенов (Refresh Token Rotation)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Пробует обновить access-токен с помощью refresh-токена.
    /// При успехе сохраняет новую пару токенов в SecureStorage.
    /// При временной сетевой ошибке не очищает локальную сессию.
    /// </summary>
    /// <returns>true — токены обновлены; false — сессия истекла, нужен логин</returns>
    public async Task<bool> RefreshTokenAsync()
    {
        try
        {
            logger.LogInformation("Обновление токенов...");

            var refreshToken = await secureStorage.GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken))
            {
                logger.LogWarning("Refresh-токен отсутствует — сессия истекла");
                return false;
            }

            var refreshResult = await TryRefreshSessionAsync();
            if (refreshResult == SessionRefreshResult.Success)
            {
                logger.LogInformation("Токены успешно обновлены");
                return true;
            }

            if (refreshResult == SessionRefreshResult.Rejected)
            {
                logger.LogWarning("Refresh-токен отклонён сервером — очищаем сессию");
                await ClearLocalSessionAsync();
                return false;
            }

            logger.LogWarning("Refresh временно недоступен; локальная сессия не очищается.");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении токенов: {Message}", ex.Message);
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Выход
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Выполняет полный логаут: отзывает refresh-токен на сервере
    /// и очищает локальное хранилище.
    /// </summary>
    public async Task<bool> LogoutAsync()
    {
        try
        {
            logger.LogInformation("Выход из аккаунта");

            // Отзываем refresh-токен на сервере, чтобы он не мог использоваться повторно
            var refreshToken = await secureStorage.GetRefreshTokenAsync();
            if (!string.IsNullOrEmpty(refreshToken))
            {
                try
                {
                    await apiClient.LogoutAsync(refreshToken);
                    logger.LogInformation("Refresh-токен отозван на сервере");
                }
                catch (Exception apiEx)
                {
                    // Ошибка сервера не должна блокировать локальный логаут
                    logger.LogWarning("Не удалось отозвать токен на сервере: {Message}", apiEx.Message);
                }
            }

            // Очищаем всё локальное состояние
            secureStorage.ClearAuthTokens();
            await storageService.DeleteAsync(CurrentUserIdKey);
            await storageService.DeleteAsync(OfflineSessionVerifiedAtKey);

            logger.LogInformation("Выход успешен");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при выходе: {Message}", ex.Message);
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Email-верификация
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Проверяет, верифицирован ли email текущего пользователя.
    /// </summary>
    public async Task<bool> IsEmailVerifiedAsync()
    {
        try
        {
            var storedFlag = await storageService.GetAsync(EmailVerifiedKey);
            if (string.Equals(storedFlag, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var user = await GetCurrentUserAsync();
            if (user?.IsEmailVerified == true)
            {
                await storageService.SaveAsync(EmailVerifiedKey, "true");
                return true;
            }

            var accessToken = await secureStorage.GetAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                logger.LogInformation("Локальный флаг email не найден, но есть активный токен. Считаем email подтвержденным.");
                await storageService.SaveAsync(EmailVerifiedKey, "true");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при проверке верификации email: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Подтверждает email с помощью кода верификации.
    /// </summary>
    public async Task<VerifyCodeResponse> VerifyEmailAsync(string email, string verificationCode)
    {
        try
        {
            logger.LogInformation("Подтверждение email для {Email}", email);

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var response = await apiClient.VerifyEmailAsync(normalizedEmail, verificationCode);

            if (response.IsValid)
            {
                var accessToken = response.AccessToken ?? response.Token;
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    await secureStorage.SaveAuthTokenAsync(accessToken, response.RefreshToken);
                }

                if (!string.IsNullOrWhiteSpace(response.UserId))
                {
                    await storageService.SaveAsync(CurrentUserIdKey, response.UserId);
                }

                await storageService.SaveAsync(CurrentEmailKey, normalizedEmail);
                await storageService.SaveAsync(EmailVerifiedKey, "true");
                // Обновляем флаг верификации у локального пользователя
                var user = await GetCurrentUserAsync();
                if (user is not null)
                {
                    user.IsEmailVerified = true;
                    await userRepository.UpdateUserWithEncryptionAsync(user);
                }

                logger.LogInformation("Email подтверждён");
                return response;
            }

            logger.LogWarning("Ошибка подтверждения: {Message}", response.Message);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при подтверждении email: {Message}", ex.Message);
            return new VerifyCodeResponse
            {
                IsValid = false,
                Success = false,
                Message = "Ошибка проверки кода. Проверьте интернет.",
                ErrorMessage = "Ошибка проверки кода. Проверьте интернет."
            };
        }
    }

    /// <summary>
    /// Отправляет код подтверждения на указанный email.
    /// </summary>
    public async Task<bool> SendEmailVerificationCodeAsync(string email)
    {
        try
        {
            logger.LogInformation("Отправка кода подтверждения на {Email}", email);

            var result = await apiClient.SendEmailVerificationCodeAsync(email);

            if (result)
                logger.LogInformation("Код подтверждения отправлен на {Email}", email);
            else
                logger.LogWarning("Ошибка отправки кода");

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отправке кода: {Message}", ex.Message);
            return false;
        }
    }
}
