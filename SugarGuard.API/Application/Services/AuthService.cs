using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.Security;
using SugarGuard.Application.Audit;
using SugarGuard.Application.Security;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;
using System.Security.Cryptography;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Сервис авторизации
/// </summary>
public class AuthService : IAuthService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IPasswordVerificationService _passwordVerification;
    private readonly IAuditService _audit;
    private readonly ICryptoService _crypto;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IDbContextFactory<AppDbContext> dbFactory,
        IPasswordVerificationService passwordVerification,
        IAuditService audit,
        ICryptoService crypto,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _dbFactory = dbFactory;
        _passwordVerification = passwordVerification;
        _audit = audit;
        _crypto = crypto;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AuthRegistrationResult> RegisterParentAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
        => await RegisterAsync(email, password, UserRole.Parent, cancellationToken);

    /// <inheritdoc/>
    public async Task<AuthRegistrationResult> RegisterAsync(
        string email,
        string password,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        if (role is UserRole.Admin or UserRole.SupportAdmin or UserRole.ServiceAccount)
        {
            return new AuthRegistrationResult(
                null,
                false,
                "role_not_allowed",
                "Registration for this role is not allowed.");
        }

        var emailForLogin = email.Trim().ToLowerInvariant();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existingUser = await db.Users
            .FirstOrDefaultAsync(u => u.EmailForLogin == emailForLogin, cancellationToken);

        if (existingUser is not null)
        {
            if (existingUser is { IsActive: true, IsEmailVerified: false })
            {
                var retryCredentials = HashPassword(password);
                existingUser.PasswordHash = retryCredentials.HashBase64;
                existingUser.PasswordSalt = retryCredentials.SaltBase64;
                existingUser.Role = role;
                existingUser.OnboardingCompleted = false;
                existingUser.OnboardingCurrentStep = Math.Max(existingUser.OnboardingCurrentStep, 1);

                await db.SaveChangesAsync(cancellationToken);

                await _audit.WriteAsync(
                    "auth.register.retry_unverified",
                    "User",
                    existingUser.UserId.ToString(),
                    "verification_resent",
                    CancellationToken.None);

                _logger.LogInformation(
                    "Registration retried for unverified user. UserId={UserId}",
                    existingUser.UserId);

                return new AuthRegistrationResult(existingUser, true, null, null);
            }

            await _audit.WriteAsync(
                "auth.register.failed",
                "User",
                existingUser.UserId.ToString(),
                "email_already_registered",
                CancellationToken.None);

            return new AuthRegistrationResult(
                null,
                false,
                "email_already_registered",
                "Email already registered.");
        }

        var credentials = HashPassword(password);
        var now = DateTime.UtcNow;

        var user = new User
        {
            EmailForLogin = emailForLogin,
            EncryptedEmail = _crypto.Encrypt(emailForLogin),
            PasswordHash = credentials.HashBase64,
            PasswordSalt = credentials.SaltBase64,
            Role = role,
            IsActive = true,
            IsEmailVerified = false,
            OnboardingCompleted = false,
            OnboardingCurrentStep = 1,
            OnboardingStartedAt = now,
            CreatedAt = now
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            "auth.register.success",
            "User",
            user.UserId.ToString(),
            $"role={role}",
            CancellationToken.None);

        _logger.LogInformation("User registered. UserId={UserId} Role={Role}", user.UserId, role);

        return new AuthRegistrationResult(user, true, null, null);
    }

    /// <inheritdoc/>
    public async Task<AuthEmailVerificationResult> ConfirmEmailAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        var emailForLogin = email.Trim().ToLowerInvariant();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.EmailForLogin == emailForLogin, cancellationToken);

        if (user is null)
        {
            return new AuthEmailVerificationResult(
                null,
                false,
                "user_not_found",
                "User not found.");
        }

        user.IsEmailVerified = true;
        user.EmailVerifiedAt = DateTime.UtcNow;

        if (user.OnboardingCurrentStep < 2)
            user.OnboardingCurrentStep = 2;

        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            "auth.email_verified",
            "User",
            user.UserId.ToString(),
            null,
            CancellationToken.None);

        _logger.LogInformation(
            "Email confirmed. UserId={UserId} VerificationTokenPrefix={TokenPrefix}",
            user.UserId,
            verificationToken.Length > 8 ? verificationToken[..8] : verificationToken);

        return new AuthEmailVerificationResult(user, true, null, null);
    }

    /// <inheritdoc/>
    public async Task<LoginResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var emailForLogin = email.Trim().ToLowerInvariant();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.EmailForLogin == emailForLogin, cancellationToken);

        if (user is null)
        {
            await _audit.WriteAsync("auth.login.failed", "User", null,
                $"email={emailForLogin}", CancellationToken.None);
            return new LoginResult(null, LoginFailureReason.UserNotFound);
        }

        if (!user.IsActive)
        {
            await _audit.WriteAsync("auth.login.failed", "User", user.UserId.ToString(),
                "account_deactivated", CancellationToken.None);
            _logger.LogWarning(
                "Попытка входа деактивированного аккаунта. UserId={UserId}", user.UserId);
            return new LoginResult(null, LoginFailureReason.AccountDeactivated);
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt))
        {
            await _audit.WriteAsync("auth.login.failed", "User", user.UserId.ToString(),
                "password_not_configured", CancellationToken.None);
            return new LoginResult(null, LoginFailureReason.PasswordNotConfigured);
        }

        if (!_passwordVerification.VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
        {
            await _audit.WriteAsync("auth.login.failed", "User", user.UserId.ToString(),
                "password_mismatch", CancellationToken.None);
            return new LoginResult(null, LoginFailureReason.PasswordMismatch);
        }

        // Проверяем подтверждение email
        if (!user.IsEmailVerified
            && user.Role != UserRole.ServiceAccount
            && user.Role != UserRole.Admin
            && user.Role != UserRole.SupportAdmin)
        {
            await _audit.WriteAsync("auth.login.failed", "User", user.UserId.ToString(),
                "email_not_verified", CancellationToken.None);
            _logger.LogWarning(
                "Вход заблокирован: email не подтверждён. UserId={UserId}", user.UserId);
            return new LoginResult(null, LoginFailureReason.EmailNotVerified);
        }

        await _audit.WriteAsync("auth.login.success", "User", user.UserId.ToString(),
            $"role={user.Role}", CancellationToken.None);
        _logger.LogInformation(
            "Успешный вход. UserId={UserId} Role={Role}", user.UserId, user.Role);

        return new LoginResult(user, LoginFailureReason.None);
    }

    /// <inheritdoc/>
    public async Task<(User? User, bool IsActive)> GetUserForRefreshAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
            return (null, false);

        return (user, user.IsActive);
    }

    /// <inheritdoc/>
    public Task WriteRefreshSuccessAuditAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return _audit.WriteAsync("auth.refresh.success", "User", userId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task WriteRefreshFailedAuditAsync(
        string userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return _audit.WriteAsync("auth.refresh.failed", "User", userId, reason, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<User?> FindActiveUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var emailForLogin = email.Trim().ToLowerInvariant();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.EmailForLogin == emailForLogin, cancellationToken);

        return user is { IsActive: true } ? user : null;
    }

    /// <inheritdoc/>
    public Task WriteForgotPasswordAuditAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return _audit.WriteAsync("auth.forgot-password", "User", userId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ResetPasswordResult> ResetPasswordAsync(
        string email,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var emailForLogin = email.Trim().ToLowerInvariant();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.EmailForLogin == emailForLogin, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return new ResetPasswordResult(null, false, "Пользователь не найден.");
        }

        // Хешируем новый пароль
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
            newPassword, salt, 600_000,
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);

        user.PasswordHash = Convert.ToBase64String(hash);
        user.PasswordSalt = Convert.ToBase64String(salt);

        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync("auth.reset-password.success", "User",
            user.UserId.ToString(), null, CancellationToken.None);

        _logger.LogInformation("Пароль сброшен. UserId={UserId}", user.UserId);

        return new ResetPasswordResult(user, true, null);
    }

    /// <inheritdoc/>
    public async Task<User> GetOrCreateServiceAccountAsync(
        CancellationToken cancellationToken = default)
    {
        const string botEmail = "bot.service@sugarguard.local";

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var serviceAccountUser = await db.Users
            .FirstOrDefaultAsync(u => u.EmailForLogin == botEmail, cancellationToken);

        if (serviceAccountUser is not null)
            return serviceAccountUser;

        serviceAccountUser = new User
        {
            EmailForLogin = botEmail,
            Role = UserRole.ServiceAccount,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsEmailVerified = true   // сервисный аккаунт верифицирован по умолчанию
        };
        db.Users.Add(serviceAccountUser);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            serviceAccountUser = await db.Users
                .FirstOrDefaultAsync(u => u.EmailForLogin == botEmail, cancellationToken)
                ?? throw new InvalidOperationException("ServiceAccount creation failed after conflict.");
        }

        return serviceAccountUser;
    }

    /// <inheritdoc/>
    public bool? ValidateBotApiKey(string? providedKey)
    {
        var expectedApiKey = Environment.GetEnvironmentVariable("BOT_SERVICE_AUTH_KEY")
            ?? _configuration["BotAuth:ApiKey"];

        if (string.IsNullOrWhiteSpace(expectedApiKey))
            return null; // не настроен

        if (string.IsNullOrEmpty(providedKey))
            return false;

        return string.Equals(providedKey, expectedApiKey, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public Task WriteBotLoginAuditAsync(
        bool success,
        string? serviceAccountUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        return _audit.WriteAsync(
            success ? "auth.bot_login.success" : "auth.bot_login.failed",
            "ServiceAccount",
            serviceAccountUserId,
            reason,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task WriteLogoutAuditAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        return _audit.WriteAsync("auth.logout", "User", userId, null, cancellationToken);
    }

    private static (string HashBase64, string SaltBase64) HashPassword(string password)
    {
        const int saltSize = 16;
        const int hashSize = 32;

        var salt = RandomNumberGenerator.GetBytes(saltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            600_000,
            HashAlgorithmName.SHA256);

        var hash = pbkdf2.GetBytes(hashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }
}
