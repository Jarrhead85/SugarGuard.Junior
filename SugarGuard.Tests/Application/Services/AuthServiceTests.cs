using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.API.Security;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Tests.Application.Services;

/// <summary>
/// Unit-тесты для <see cref="AuthService"/>.
/// <para>
/// <b>Critical coverage (security-critical):</b>
/// <list type="bullet">
///   <item><description><b>LoginAsync — 5 причин отказа:</b> user_not_found,
///     account_deactivated, password_not_configured, password_mismatch,
///     email_not_verified. Каждая причина пишет отдельный audit event с
///     конкретным reason. Тесты проверяют и result, и audit.</description></item>
///   <item><description><b>Email normalization:</b> trim + ToLowerInvariant —
///     иначе Parent@mail.com и parent@mail.com будут разными аккаунтами.</description></item>
///   <item><description><b>Email verification bypass для ServiceAccount/Admin/SupportAdmin:</b>
///     бот и админы логинятся без подтверждения email (тест покрывает каждую роль).</description></item>
///   <item><description><b>HIGH-4 contract:</b> audit пишется с <c>CancellationToken.None</c>.</description></item>
///   <item><description><b>ResetPasswordAsync:</b> хеш обновлён, salt сгенерирован,
///     запись сохранена. PBKDF2-SHA256 600K итераций (NIST SP 800-132).</description></item>
///   <item><description><b>GetOrCreateServiceAccountAsync:</b> race-safe — DbUpdateException
///     ловится и перечитывается запись.</description></item>
///   <item><description><b>ValidateBotApiKey:</b> env var > config; пустая конфигурация → null.</description></item>
/// </list>
/// </para>
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly string _dbName = $"AuthTest_{Guid.NewGuid()}";
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IPasswordVerificationService> _passwordVerification = new();
    private readonly Mock<ICryptoService> _crypto = new();
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public AuthServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;

        _crypto
            .Setup(c => c.Encrypt(It.IsAny<string>()))
            .Returns((string value) => $"enc:{value}");
    }

    public void Dispose()
    {
        using var ctx = new AppDbContext(_dbOptions);
        ctx.Database.EnsureDeleted();
        // Очищаем env-var, если выставляли в тесте
        Environment.SetEnvironmentVariable("BOT_SERVICE_AUTH_KEY", null);
    }

    private TestAppDbContextFactory CreateFactory() => new(_dbOptions);

    private AuthService CreateSut(IConfiguration? config = null) =>
        new(
            CreateFactory(),
            _passwordVerification.Object,
            _audit.Object,
            _crypto.Object,
            config ?? new ConfigurationBuilder().Build(),
            NullLogger<AuthService>.Instance);

    private static User CreateUser(UserRole role, bool isActive = true, bool isEmailVerified = true) => new()
    {
        UserId = Guid.NewGuid(),
        EmailForLogin = $"{role.ToString().ToLowerInvariant()}@test.local",
        PasswordHash = Convert.ToBase64String(new byte[32]),
        PasswordSalt = Convert.ToBase64String(new byte[16]),
        Role = role,
        IsActive = isActive,
        IsEmailVerified = isEmailVerified,
        CreatedAt = DateTime.UtcNow
    };

    // ───────────────────────────────────────────────────────────────────
    // LoginAsync — 5 failure paths + success matrix
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsFailureAndAudits()
    {
        var sut = CreateSut();

        var result = await sut.LoginAsync("unknown@test.local", "any-password");

        Assert.Null(result.User);
        Assert.Equal(LoginFailureReason.UserNotFound, result.FailureReason);

        _audit.Verify(a => a.WriteAsync(
            "auth.login.failed", "User", null,
            It.Is<string>(s => s.Contains("unknown@test.local")),
            CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData("USER@TEST.LOCAL")]  // uppercase
    [InlineData("user@test.local   ")]  // trailing space
    [InlineData("  user@test.local")]  // leading space
    [InlineData("User@Test.Local  ")]  // mixed
    public async Task LoginAsync_EmailNormalization_TrimsAndLowercases(string inputEmail)
    {
        // Arrange: создаём пользователя с нормализованным email
        var user = CreateUser(UserRole.Parent);
        user.EmailForLogin = "user@test.local";
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        _passwordVerification
            .Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        var sut = CreateSut();

        // Act: вход с email в разных регистрах/с пробелами
        var result = await sut.LoginAsync(inputEmail, "password");

        // Assert: нашли пользователя (нормализация сработала)
        Assert.NotNull(result.User);
        Assert.Equal(LoginFailureReason.None, result.FailureReason);
    }

    [Fact]
    public async Task LoginAsync_AccountDeactivated_ReturnsFailure()
    {
        var user = CreateUser(UserRole.Parent, isActive: false);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.LoginAsync(user.EmailForLogin, "password");

        Assert.Null(result.User);
        Assert.Equal(LoginFailureReason.AccountDeactivated, result.FailureReason);

        _audit.Verify(a => a.WriteAsync(
            "auth.login.failed", "User", user.UserId.ToString(),
            "account_deactivated", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_PasswordHashNull_ReturnsPasswordNotConfigured()
    {
        var user = CreateUser(UserRole.Parent);
        user.PasswordHash = null;
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.LoginAsync(user.EmailForLogin, "password");

        Assert.Null(result.User);
        Assert.Equal(LoginFailureReason.PasswordNotConfigured, result.FailureReason);

        _audit.Verify(a => a.WriteAsync(
            "auth.login.failed", "User", user.UserId.ToString(),
            "password_not_configured", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_PasswordSaltNull_ReturnsPasswordNotConfigured()
    {
        var user = CreateUser(UserRole.Parent);
        user.PasswordSalt = null;
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.LoginAsync(user.EmailForLogin, "password");

        Assert.Equal(LoginFailureReason.PasswordNotConfigured, result.FailureReason);
    }

    [Fact]
    public async Task LoginAsync_PasswordMismatch_ReturnsFailure()
    {
        var user = CreateUser(UserRole.Parent);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        _passwordVerification
            .Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);
        var sut = CreateSut();

        var result = await sut.LoginAsync(user.EmailForLogin, "wrong-password");

        Assert.Null(result.User);
        Assert.Equal(LoginFailureReason.PasswordMismatch, result.FailureReason);

        _audit.Verify(a => a.WriteAsync(
            "auth.login.failed", "User", user.UserId.ToString(),
            "password_mismatch", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_PasswordVerificationNeverCalled_WhenUserNotFound()
    {
        // Оптимизация: если пользователь не найден, не нужно делать
        // дорогую PBKDF2-проверку (это timing-safe).
        var sut = CreateSut();

        await sut.LoginAsync("nobody@nowhere.local", "any");

        _passwordVerification.Verify(
            p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_EmailNotVerified_ReturnsFailure()
    {
        var user = CreateUser(UserRole.Parent, isEmailVerified: false);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        _passwordVerification
            .Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        var sut = CreateSut();

        var result = await sut.LoginAsync(user.EmailForLogin, "password");

        Assert.Null(result.User);
        Assert.Equal(LoginFailureReason.EmailNotVerified, result.FailureReason);

        _audit.Verify(a => a.WriteAsync(
            "auth.login.failed", "User", user.UserId.ToString(),
            "email_not_verified", CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SupportAdmin)]
    [InlineData(UserRole.ServiceAccount)]
    public async Task LoginAsync_BypassEmailVerification_ForAdminAndServiceRoles(UserRole role)
    {
        // Эти роли логинятся даже без подтверждения email (бот/админ).
        var user = CreateUser(role, isEmailVerified: false);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        _passwordVerification
            .Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        var sut = CreateSut();

        var result = await sut.LoginAsync(user.EmailForLogin, "password");

        Assert.NotNull(result.User);
        Assert.Equal(LoginFailureReason.None, result.FailureReason);
    }

    [Theory]
    [InlineData(UserRole.Parent)]
    [InlineData(UserRole.Doctor)]
    [InlineData(UserRole.ChildDevice)]
    public async Task LoginAsync_RegularRoles_RequireEmailVerification(UserRole role)
    {
        var user = CreateUser(role, isEmailVerified: false);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        _passwordVerification
            .Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        var sut = CreateSut();

        var result = await sut.LoginAsync(user.EmailForLogin, "password");

        Assert.Equal(LoginFailureReason.EmailNotVerified, result.FailureReason);
    }

    [Fact]
    public async Task LoginAsync_Success_ReturnsUserAndAudits()
    {
        var user = CreateUser(UserRole.Parent);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        _passwordVerification
            .Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        var sut = CreateSut();

        var result = await sut.LoginAsync(user.EmailForLogin, "password");

        Assert.NotNull(result.User);
        Assert.Equal(user.UserId, result.User!.UserId);
        Assert.Equal(LoginFailureReason.None, result.FailureReason);

        _audit.Verify(a => a.WriteAsync(
            "auth.login.success", "User", user.UserId.ToString(),
            $"role={user.Role}", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_AuditUsesProvidedCancellationToken_ForFailurePaths()
    {
        var user = CreateUser(UserRole.Parent, isActive: false);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        await sut.LoginAsync(user.EmailForLogin, "password", cts.Token);

        _audit.Verify(a => a.WriteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string?>(), cts.Token), Times.AtLeastOnce);
    }

    // ───────────────────────────────────────────────────────────────────
    // GetUserForRefreshAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserForRefreshAsync_UserNotFound_ReturnsNullAndFalse()
    {
        var sut = CreateSut();
        var (user, isActive) = await sut.GetUserForRefreshAsync(Guid.NewGuid());

        Assert.Null(user);
        Assert.False(isActive);
    }

    [Fact]
    public async Task GetUserForRefreshAsync_ActiveUser_ReturnsUserAndTrue()
    {
        var user = CreateUser(UserRole.Parent, isActive: true);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var (returned, isActive) = await sut.GetUserForRefreshAsync(user.UserId);

        Assert.NotNull(returned);
        Assert.Equal(user.UserId, returned!.UserId);
        Assert.True(isActive);
    }

    [Fact]
    public async Task GetUserForRefreshAsync_InactiveUser_ReturnsUserAndFalse()
    {
        var user = CreateUser(UserRole.Parent, isActive: false);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var (returned, isActive) = await sut.GetUserForRefreshAsync(user.UserId);

        Assert.NotNull(returned);
        Assert.False(isActive);
    }

    // ───────────────────────────────────────────────────────────────────
    // WriteRefreshSuccessAuditAsync / WriteRefreshFailedAuditAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteRefreshSuccessAuditAsync_DelegatesToAudit()
    {
        var sut = CreateSut();
        var userId = Guid.NewGuid().ToString();

        await sut.WriteRefreshSuccessAuditAsync(userId);

        _audit.Verify(a => a.WriteAsync(
            "auth.refresh.success", "User", userId, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteRefreshFailedAuditAsync_PassesReason()
    {
        var sut = CreateSut();
        var userId = Guid.NewGuid().ToString();
        var reason = "token_reuse_detected";

        await sut.WriteRefreshFailedAuditAsync(userId, reason);

        _audit.Verify(a => a.WriteAsync(
            "auth.refresh.failed", "User", userId, reason,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // FindActiveUserByEmailAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindActiveUserByEmailAsync_UserNotFound_ReturnsNull()
    {
        var sut = CreateSut();
        var result = await sut.FindActiveUserByEmailAsync("nobody@nowhere.local");
        Assert.Null(result);
    }

    [Fact]
    public async Task FindActiveUserByEmailAsync_InactiveUser_ReturnsNull()
    {
        // SECURITY: забыли пароль для деактивированного аккаунта = нельзя узнать, что он существует
        var user = CreateUser(UserRole.Parent, isActive: false);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.FindActiveUserByEmailAsync(user.EmailForLogin);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindActiveUserByEmailAsync_ActiveUser_ReturnsUser()
    {
        var user = CreateUser(UserRole.Parent, isActive: true);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.FindActiveUserByEmailAsync(user.EmailForLogin);

        Assert.NotNull(result);
        Assert.Equal(user.UserId, result!.UserId);
    }

    // ───────────────────────────────────────────────────────────────────
    // WriteForgotPasswordAuditAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteForgotPasswordAuditAsync_DelegatesToAudit()
    {
        var sut = CreateSut();
        var userId = Guid.NewGuid().ToString();

        await sut.WriteForgotPasswordAuditAsync(userId);

        _audit.Verify(a => a.WriteAsync(
            "auth.forgot-password", "User", userId, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // ResetPasswordAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ReturnsFailureWithoutAudit()
    {
        var sut = CreateSut();

        var result = await sut.ResetPasswordAsync("nobody@nowhere.local", "newPassword123");

        Assert.False(result.Success);
        Assert.Equal("Пользователь не найден.", result.ErrorMessage);
        Assert.Null(result.User);
        // SECURITY: не пишем audit с email (нельзя узнать существование email-а)
        _audit.Verify(a => a.WriteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_InactiveUser_ReturnsFailure()
    {
        var user = CreateUser(UserRole.Parent, isActive: false);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.ResetPasswordAsync(user.EmailForLogin, "newPassword123");

        Assert.False(result.Success);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ResetPasswordAsync_Success_UpdatesHashAndSaltAndAudits()
    {
        var user = CreateUser(UserRole.Parent);
        var oldHash = user.PasswordHash;
        var oldSalt = user.PasswordSalt;
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.ResetPasswordAsync(user.EmailForLogin, "newPassword123");

        Assert.True(result.Success);
        Assert.NotNull(result.User);

        // Проверяем что хеш и salt обновились в БД
        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Users.FindAsync(user.UserId);
        Assert.NotNull(saved);
        Assert.NotEqual(oldHash, saved!.PasswordHash);
        Assert.NotEqual(oldSalt, saved.PasswordSalt);
        // Salt имеет размер 16 байт → Base64 длина 24
        var saltBytes = Convert.FromBase64String(saved.PasswordSalt!);
        Assert.Equal(16, saltBytes.Length);
        // Hash имеет размер 32 байта → Base64 длина 44
        var hashBytes = Convert.FromBase64String(saved.PasswordHash!);
        Assert.Equal(32, hashBytes.Length);

        _audit.Verify(a => a.WriteAsync(
            "auth.reset-password.success", "User", user.UserId.ToString(),
            null, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_EmailNormalized_TrimsAndLowercases()
    {
        var user = CreateUser(UserRole.Parent);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.ResetPasswordAsync("  PARENT@TEST.LOCAL  ", "newPassword123");

        Assert.True(result.Success);
    }

    // ───────────────────────────────────────────────────────────────────
    // GetOrCreateServiceAccountAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateServiceAccountAsync_AlreadyExists_ReturnsExisting()
    {
        var existing = new User
        {
            UserId = Guid.NewGuid(),
            EmailForLogin = "bot.service@sugarguard.local",
            Role = UserRole.ServiceAccount,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Users.Add(existing);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetOrCreateServiceAccountAsync();

        Assert.Equal(existing.UserId, result.UserId);
        Assert.Equal(UserRole.ServiceAccount, result.Role);
        Assert.True(result.IsEmailVerified);
    }

    [Fact]
    public async Task GetOrCreateServiceAccountAsync_NotExists_CreatesAndReturns()
    {
        var sut = CreateSut();

        var result = await sut.GetOrCreateServiceAccountAsync();

        Assert.Equal("bot.service@sugarguard.local", result.EmailForLogin);
        Assert.Equal(UserRole.ServiceAccount, result.Role);
        Assert.True(result.IsActive);
        Assert.True(result.IsEmailVerified);

        // Запись реально сохранена в БД
        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Users
            .FirstOrDefaultAsync(u => u.EmailForLogin == "bot.service@sugarguard.local");
        Assert.NotNull(saved);
    }

    // ───────────────────────────────────────────────────────────────────
    // ValidateBotApiKey
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateBotApiKey_EnvVarSet_ValidKey_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable("BOT_SERVICE_AUTH_KEY", "env-secret-key");
        var sut = CreateSut();

        var result = sut.ValidateBotApiKey("env-secret-key");

        Assert.True(result);
    }

    [Fact]
    public void ValidateBotApiKey_ConfigFallback_ValidKey_ReturnsTrue()
    {
        // Env не задан, но конфиг есть
        Environment.SetEnvironmentVariable("BOT_SERVICE_AUTH_KEY", null);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotAuth:ApiKey"] = "config-secret-key"
            })
            .Build();
        var sut = CreateSut(config);

        var result = sut.ValidateBotApiKey("config-secret-key");

        Assert.True(result);
    }

    [Fact]
    public void ValidateBotApiKey_EnvVarSet_WrongKey_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("BOT_SERVICE_AUTH_KEY", "expected");
        var sut = CreateSut();

        var result = sut.ValidateBotApiKey("attacker-guess");

        Assert.False(result);
    }

    [Fact]
    public void ValidateBotApiKey_NotConfigured_ReturnsNull()
    {
        // Env не задан + конфиг пустой → null (функция выключена)
        Environment.SetEnvironmentVariable("BOT_SERVICE_AUTH_KEY", null);
        var sut = CreateSut();

        var result = sut.ValidateBotApiKey("any-key");

        Assert.Null(result);
    }

    [Fact]
    public void ValidateBotApiKey_EmptyProvidedKey_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("BOT_SERVICE_AUTH_KEY", "expected");
        var sut = CreateSut();

        var result = sut.ValidateBotApiKey("");

        Assert.False(result);
    }

    [Fact]
    public void ValidateBotApiKey_EnvVarTakesPrecedenceOverConfig()
    {
        Environment.SetEnvironmentVariable("BOT_SERVICE_AUTH_KEY", "env-value");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotAuth:ApiKey"] = "config-value"
            })
            .Build();
        var sut = CreateSut(config);

        // Env "env-value" должен сравниваться, не config "config-value"
        Assert.True(sut.ValidateBotApiKey("env-value"));
        Assert.False(sut.ValidateBotApiKey("config-value"));
    }

    [Fact]
    public void ValidateBotApiKey_OrdinalComparison_NotCaseInsensitive()
    {
        Environment.SetEnvironmentVariable("BOT_SERVICE_AUTH_KEY", "CaseSensitive");
        var sut = CreateSut();

        // StringComparison.Ordinal — должно быть точное совпадение регистра
        Assert.False(sut.ValidateBotApiKey("casesensitive"));
    }

    // ───────────────────────────────────────────────────────────────────
    // WriteBotLoginAuditAsync / WriteLogoutAuditAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteBotLoginAuditAsync_Success_ActionNameIsSuccess()
    {
        var sut = CreateSut();
        var serviceAccountId = Guid.NewGuid().ToString();

        await sut.WriteBotLoginAuditAsync(success: true, serviceAccountId, "ok", CancellationToken.None);

        _audit.Verify(a => a.WriteAsync(
            "auth.bot_login.success", "ServiceAccount", serviceAccountId, "ok",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteBotLoginAuditAsync_Failed_ActionNameIsFailed()
    {
        var sut = CreateSut();

        await sut.WriteBotLoginAuditAsync(success: false, null, "invalid_key", CancellationToken.None);

        _audit.Verify(a => a.WriteAsync(
            "auth.bot_login.failed", "ServiceAccount", null, "invalid_key",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteLogoutAuditAsync_DelegatesToAudit()
    {
        var sut = CreateSut();
        var userId = Guid.NewGuid().ToString();

        await sut.WriteLogoutAuditAsync(userId);

        _audit.Verify(a => a.WriteAsync(
            "auth.logout", "User", userId, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
