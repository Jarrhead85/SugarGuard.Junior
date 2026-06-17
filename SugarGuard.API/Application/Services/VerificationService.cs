using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using SugarGuard.API.Application.Services;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Services
{
    /// <summary>
    /// Реализация верификации контактных данных
    /// </summary>

    public class VerificationService : IVerificationService
    {
        // Настройки
        private const int CodeTtlMinutes = 15; // TTL верификационного кода

        private const int TokenTtlMinutes = 30; // TTL токена подтверждения после успешной проверки

        private const int MaxAttempts = 10; // Максимальное число попыток ввода кода до аннулирования

        private const int CodeLength = 8; // Длина верификационного кода (8 цифр)

        private const int RetryAfterSeconds = 60; // Минимальный интервал между повторными отправками

        // Префиксы ключей кэша  
        private const string CodeCachePrefix = "verification:code"; // Префикс для записей верификационного кода в кэше       

        private const string TokenCachePrefix = "verification:token"; // Префикс для записей токена подтверждения в кэше       

        private const string RateLimitCachePrefix = "verification:ratelimit"; // Префикс для записей ограничения повторной отправки

        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;
        private readonly ILogger<VerificationService> _logger;

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _codeLocks = new();

        public VerificationService(
            IMemoryCache cache,
            IEmailService emailService,
            ILogger<VerificationService> logger)
        {
            _cache = cache;
            _emailService = emailService;
            _logger = logger;
        }

        private SemaphoreSlim GetOrCreateCodeLock(string codeKey)
            => _codeLocks.GetOrAdd(codeKey, static _ => new SemaphoreSlim(1, 1));

        /// <inheritdoc/>
        public async Task<SendVerificationCodeResult> SendCodeAsync(
            string email,
            VerificationPurpose purpose,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
                return SendVerificationCodeResult.Fail(
                    "email_invalid", "Email не может быть пустым.");

            var normalizedEmail = email.Trim().ToLowerInvariant();

            // Защита от спама
            var rateLimitKey = $"{RateLimitCachePrefix}:{normalizedEmail}";
            if (_cache.TryGetValue(rateLimitKey, out _))
            {
                _logger.LogWarning(
                    "Повторная отправка кода заблокирована (rate limit). Email={Email}", normalizedEmail);
                return SendVerificationCodeResult.Fail(
                    "too_many_requests",
                    $"Повторная отправка доступна через {RetryAfterSeconds} секунд.");
            }

            // Генерируем 8-значный числовой код
            var code = ConnectionCodeFormat.Generate();
            var expiresAt = DateTime.UtcNow.AddMinutes(CodeTtlMinutes);

            // Сохраняем запись кода в кэше
            var codeKey = BuildCodeKey(purpose, normalizedEmail);
            var codeEntry = new VerificationCodeEntry
            {
                Code = code,
                ExpiresAt = expiresAt,
                AttemptsLeft = MaxAttempts,
                IsUsed = false
            };

            _cache.Set(codeKey, codeEntry, new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = expiresAt,
                Priority = CacheItemPriority.Normal
            });

            _cache.Set(rateLimitKey, true, TimeSpan.FromSeconds(RetryAfterSeconds));

            // Отправляем письмо
            try
            {
                var subject = purpose switch
                {
                    VerificationPurpose.Registration => "SugarGuard — подтверждение регистрации",
                    VerificationPurpose.PasswordReset => "SugarGuard — сброс пароля",
                    VerificationPurpose.EmailChange => "SugarGuard — подтверждение нового email",
                    _ => "SugarGuard — код подтверждения"
                };

                var body = BuildEmailBody(code, purpose, expiresAt);

                await _emailService.SendAsync(normalizedEmail, subject, body, cancellationToken);

                _logger.LogInformation(
                    "Верификационный код отправлен. Email={Email} Purpose={Purpose} ExpiresAt={ExpiresAt}",
                    normalizedEmail, purpose, expiresAt);

                return SendVerificationCodeResult.Ok(expiresAt);
            }
            catch (Exception ex)
            {
                _cache.Remove(codeKey);
                _cache.Remove(rateLimitKey);

                _logger.LogError(ex,
                    "Ошибка отправки верификационного кода. Email={Email}", normalizedEmail);

                return SendVerificationCodeResult.Fail(
                    "send_failed",
                    "Не удалось отправить письмо. Попробуйте ещё раз.");
            }
        }

        /// <inheritdoc/>
        public async Task<VerifyCodeResult> VerifyCodeAsync(
            string email,
            string code,
            VerificationPurpose purpose,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return VerifyCodeResult.Fail("code_invalid", "Email или код не могут быть пустыми.");

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var normalizedCode = ConnectionCodeFormat.Normalize(code);
            if (normalizedCode is null)
                return VerifyCodeResult.Fail("code_invalid", "Неверный формат кода.");

            var codeKey = BuildCodeKey(purpose, normalizedEmail);

            var codeLock = GetOrCreateCodeLock(codeKey);
            await codeLock.WaitAsync(cancellationToken);
            try
            {
                // Ищем запись в кэше
                if (!_cache.TryGetValue(codeKey, out VerificationCodeEntry? entry) || entry is null)
                {
                    _logger.LogWarning(
                        "Код не найден в кэше. Email={Email} Purpose={Purpose}", normalizedEmail, purpose);
                    return VerifyCodeResult.Fail(
                        "code_not_found",
                        "Код не найден или истёк. Запросите новый.");
                }

                if (entry.ExpiresAt <= DateTime.UtcNow)
                {
                    _cache.Remove(codeKey);
                    _logger.LogWarning(
                        "Код истёк. Email={Email} Purpose={Purpose}", normalizedEmail, purpose);
                    return VerifyCodeResult.Fail("code_expired", "Срок действия кода истёк.");
                }

                // Проверяем одноразовость
                if (entry.IsUsed)
                {
                    _logger.LogWarning(
                        "Попытка повторного использования кода. Email={Email}", normalizedEmail);
                    return VerifyCodeResult.Fail("already_used", "Код уже был использован.");
                }

                // Проверяем счётчик попыток
                if (entry.AttemptsLeft <= 0)
                {
                    _cache.Remove(codeKey);
                    _logger.LogWarning(
                        "Исчерпаны попытки ввода кода. Email={Email}", normalizedEmail);
                    return VerifyCodeResult.Fail(
                        "attempts_exceeded",
                        "Превышено число попыток. Запросите новый код.");
                }

                // Сравниваем код
                if (!CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(entry.Code),
                        System.Text.Encoding.UTF8.GetBytes(normalizedCode)))
                {
                    entry.AttemptsLeft--;

                    // Обновляем запись в кэше с уменьшенным счётчиком
                    _cache.Set(codeKey, entry, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpiration = entry.ExpiresAt
                    });

                    var attemptsLeft = entry.AttemptsLeft;

                    if (attemptsLeft <= 0)
                    {
                        _cache.Remove(codeKey);
                        _logger.LogWarning(
                            "Код аннулирован после исчерпания попыток. Email={Email}", normalizedEmail);
                        return VerifyCodeResult.Fail(
                            "attempts_exceeded",
                            "Неверный код. Все попытки исчерпаны. Запросите новый код.",
                            attemptsLeft: 0);
                    }

                    _logger.LogWarning(
                        "Неверный код. Email={Email} AttemptsLeft={Left}", normalizedEmail, attemptsLeft);
                    return VerifyCodeResult.Fail(
                        "code_invalid",
                        $"Неверный код. Осталось попыток: {attemptsLeft}.",
                        attemptsLeft: attemptsLeft);
                }

                entry.IsUsed = true;
                _cache.Set(codeKey, entry, new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = DateTime.UtcNow.AddMinutes(1)
                });

                // Генерируем токен подтверждения
                var verificationToken = GenerateVerificationToken();
                var tokenKey = BuildTokenKey(purpose, normalizedEmail);

                _cache.Set(tokenKey, verificationToken, new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = DateTime.UtcNow.AddMinutes(TokenTtlMinutes)
                });

                _logger.LogInformation(
                    "Email успешно верифицирован. Email={Email} Purpose={Purpose}",
                    normalizedEmail, purpose);

                return VerifyCodeResult.Ok(verificationToken);
            }
            finally
            {
                codeLock.Release();
            }
        }

        /// <inheritdoc/>
        public bool IsEmailVerified(
            string email,
            string verificationToken,
            VerificationPurpose purpose)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(verificationToken))
                return false;

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var tokenKey = BuildTokenKey(purpose, normalizedEmail);

            if (!_cache.TryGetValue(tokenKey, out string? storedToken) || storedToken is null)
                return false;

            // сравнение токенов
            var isValid = CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(storedToken),
                System.Text.Encoding.UTF8.GetBytes(verificationToken));

            if (isValid)
            {
                _cache.Remove(tokenKey);
                _logger.LogInformation(
                    "Токен верификации подтверждён и удалён. Email={Email}", normalizedEmail);
            }

            return isValid;
        }

        // Приватные вспомогательные методы
        /// <summary>
        /// Строит ключ кэша для верификационного кода
        /// </summary>
        private static string BuildCodeKey(VerificationPurpose purpose, string email) =>
            $"{CodeCachePrefix}:{purpose}:{email}";

        /// <summary>
        /// Строит ключ кэша для токена подтверждения
        /// </summary>
        private static string BuildTokenKey(VerificationPurpose purpose, string email) =>
            $"{TokenCachePrefix}:{purpose}:{email}";

        /// <summary>
        /// Генерирует криптографически случайный 8-значный числовой код
        /// </summary>
        /// <summary>
        /// Генерирует случайный токен подтверждения
        /// </summary>
        private static string GenerateVerificationToken()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Формирует тело письма с кодом подтверждения
        /// </summary>
        private static string BuildEmailBody(
            string code,
            VerificationPurpose purpose,
            DateTime expiresAt)
        {
            var purposeText = purpose switch
            {
                VerificationPurpose.Registration => "завершения регистрации",
                VerificationPurpose.PasswordReset => "сброса пароля",
                VerificationPurpose.EmailChange => "подтверждения нового email",
                _ => "подтверждения действия"
            };

            var displayCode = ConnectionCodeFormat.Format(code);
            var expiresLocal = expiresAt.ToString("HH:mm UTC");

            return $"""
                    <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto;">
                      <h2 style="color: #01696f;">SugarGuard</h2>
                      <p>Для {purposeText} введите код:</p>
                      <div style="
                        font-size: 36px;
                        font-weight: bold;
                        letter-spacing: 8px;
                        color: #01696f;
                        padding: 20px;
                        background: #f7f6f2;
                        border-radius: 8px;
                        text-align: center;
                        margin: 24px 0;">
                        {displayCode}
                      </div>
                      <p style="color: #7a7974; font-size: 14px; line-height: 1.55;">
                        Код действителен до {expiresLocal}.<br/>
                        Если письмо пришло с задержкой или вы не видите новые письма от SugarGuard,
                        проверьте папку «Спам».<br/>
                        Если вы не запрашивали этот код — проигнорируйте письмо.
                      </p>
                    </div>
                    """;
        }

        // Вложенный приватный класс — запись кода в кэше
        /// <summary>
        /// Внутренняя запись верификационного кода
        /// </summary>
        private sealed class VerificationCodeEntry
        {
            public string Code { get; init; } = string.Empty; // 8-значный числовой код
           
            public DateTime ExpiresAt { get; init; } // UTC-время истечения
           
            public int AttemptsLeft { get; set; } // Количество оставшихся попыток ввода
            
            public bool IsUsed { get; set; } // Использован ли код
        }
    }
}
