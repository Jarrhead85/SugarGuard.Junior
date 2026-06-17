using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;
using IEmailService = SugarGuard.API.Services.IEmailService;

namespace SugarGuard.Tests.Application.Services;

/// <summary>
/// Unit-тесты для <see cref="RefreshTokenService"/>.
/// <para>
/// <b>H-5 (release 1.0.0):</b> критический сценарий — повторное использование
/// ротированного токена. Тест <see cref="ValidateAsync_ReusedToken_RevokesAllAndSendsThreeSignals"/>
/// проверяет, что при обнаружении факта кражи отзываются ВСЕ токены пользователя
/// И отправляются 3 независимых best-effort сигнала (audit + push + email).
/// </para>
/// </summary>
public class RefreshTokenServiceTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IWebPushService> _webPush = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly IConfiguration _configuration;

    public RefreshTokenServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenExpiryDays"] = "7"
            })
            .Build();
    }

    private RefreshTokenService CreateSut(AppDbContext context) =>
        new(context, _configuration, NullLogger<RefreshTokenService>.Instance,
            _audit.Object, _webPush.Object, _email.Object);

    private AppDbContext CreateContext() => new(_dbOptions);

    private static User CreateUser(string email = "test@example.com") => new()
    {
        UserId = Guid.NewGuid(),
        EmailForLogin = email,
        CreatedAt = DateTime.UtcNow
    };

    public void Dispose()
    {
        using var ctx = CreateContext();
        ctx.Database.EnsureDeleted();
    }

    // ───────────────────────────────────────────────────────────────────
    // CreateAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsHashAndSetsExpiryFromConfig()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);

        // Act
        var (plain, entity) = await sut.CreateAsync(
            user.UserId.ToString(), "127.0.0.1", "TestAgent", CancellationToken.None);

        // Assert
        Assert.NotEmpty(plain);
        Assert.NotEqual(plain, entity.Token); // token is hashed, not plain
        Assert.Equal(64, entity.Token.Length); // SHA-256 hex = 64 chars
        Assert.False(entity.IsRevoked);
        Assert.Null(entity.ReplacedByToken);
        Assert.Equal(user.UserId, entity.UserId);
        Assert.Equal("127.0.0.1", entity.CreatedByIp);
        Assert.Equal("TestAgent", entity.CreatedByUserAgent);

        var expectedExpiry = entity.CreatedAt.AddDays(7);
        Assert.InRange(entity.ExpiresAt,
            expectedExpiry.AddSeconds(-2),
            expectedExpiry.AddSeconds(2));
    }

    [Fact]
    public async Task CreateAsync_TruncatesUserAgentTo256Chars()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);

        var longUa = new string('A', 500);

        // Act
        var (_, entity) = await sut.CreateAsync(
            user.UserId.ToString(), null, longUa, CancellationToken.None);

        // Assert
        Assert.Equal(256, entity.CreatedByUserAgent!.Length);
    }

    [Fact]
    public async Task CreateAsync_NullUserAgentAndIp_AreAllowed()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);

        // Act
        var (_, entity) = await sut.CreateAsync(
            user.UserId.ToString(), null, null, CancellationToken.None);

        // Assert
        Assert.Null(entity.CreatedByIp);
        Assert.Null(entity.CreatedByUserAgent);
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueTokensAcrossCalls()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);

        // Act
        var (plain1, _) = await sut.CreateAsync(user.UserId.ToString(), null, null, default);
        var (plain2, _) = await sut.CreateAsync(user.UserId.ToString(), null, null, default);

        // Assert — RNG должен давать уникальные токены
        Assert.NotEqual(plain1, plain2);
    }

    // ───────────────────────────────────────────────────────────────────
    // ValidateAsync — happy path
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_KnownValidToken_ReturnsEntity()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);
        var (plain, created) = await sut.CreateAsync(
            user.UserId.ToString(), null, null, default);

        // Detach, чтобы повторный запрос пошел в БД
        ctx.Entry(created).State = EntityState.Detached;

        // Act
        var result = await sut.ValidateAsync(plain, user.UserId.ToString(), default);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
    }

    [Fact]
    public async Task ValidateAsync_UnknownToken_ReturnsNull()
    {
        // Arrange
        await using var ctx = CreateContext();
        var sut = CreateSut(ctx);

        // Act
        var result = await sut.ValidateAsync("never-issued", Guid.NewGuid().ToString(), default);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredToken_ReturnsNull()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);

        // Создаём токен и форсируем истечение
        var (plain, entity) = await sut.CreateAsync(
            user.UserId.ToString(), null, null, default);
        entity.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await ctx.SaveChangesAsync();
        ctx.Entry(entity).State = EntityState.Detached;

        // Act
        var result = await sut.ValidateAsync(plain, user.UserId.ToString(), default);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_AlreadyRevokedToken_ReturnsNull()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);
        var (plain, entity) = await sut.CreateAsync(
            user.UserId.ToString(), null, null, default);
        entity.IsRevoked = true;
        entity.RevokedReason = "logout";
        await ctx.SaveChangesAsync();
        ctx.Entry(entity).State = EntityState.Detached;

        // Act
        var result = await sut.ValidateAsync(plain, user.UserId.ToString(), default);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_TokenBelongsToOtherUser_ReturnsNull()
    {
        // Arrange
        await using var ctx = CreateContext();
        var owner = CreateUser();
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);
        var (plain, _) = await sut.CreateAsync(
            owner.UserId.ToString(), null, null, default);

        // Act — пытаемся валидировать чужой токен от имени другого пользователя
        var attackerId = Guid.NewGuid();
        var result = await sut.ValidateAsync(plain, attackerId.ToString(), default);

        // Assert
        Assert.Null(result);
    }

    // ───────────────────────────────────────────────────────────────────
    // H-5: повторное использование ротированного токена
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_ReusedToken_RevokesAllAndSendsThreeSignals()
    {
        // Arrange: токен уже ротирован (ReplacedByToken != null)
        await using var ctx = CreateContext();
        var user = CreateUser(email: "user@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);
        var (plainOld, oldToken) = await sut.CreateAsync(
            user.UserId.ToString(), "10.0.0.1", null, default);
        oldToken.ReplacedByToken = "newhash-replaces-this-one";
        oldToken.IsRevoked = true;
        oldToken.RevokedReason = "rotation";

        // Создаём второй токен в той же сессии (для последующей проверки revoke-all)
        var (_, activeToken) = await sut.CreateAsync(
            user.UserId.ToString(), "10.0.0.1", null, default);
        await ctx.SaveChangesAsync();
        ctx.Entry(oldToken).State = EntityState.Detached;
        ctx.Entry(activeToken).State = EntityState.Detached;

        // Act — атакующий пытается использовать уже ротированный токен
        var result = await sut.ValidateAsync(plainOld, user.UserId.ToString(), default);

        // Assert
        Assert.Null(result); // токен не валиден

        // Все 3 сигнала вызваны
        _audit.Verify(a => a.WriteAsync(
            "auth.refresh.reuse_detected",
            "User",
            user.UserId.ToString(),
            It.Is<string>(s => s.Contains("reuse_detected")),
            It.IsAny<CancellationToken>()), Times.Once);

        _webPush.Verify(p => p.SendNotificationAsync(
            user.UserId,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _email.Verify(e => e.SendAsync(
            "user@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Все токены пользователя отозваны.
        // Активный токен — RevokeAllForUserAsync проставляет "reuse_detected".
        // Уже отозванный (по rotation) — остаётся с "rotation" (RevokeAllForUserAsync
        // идемпотентен для уже отозванных).
        var tokensAfter = await ctx.RefreshTokens
            .Where(t => t.UserId == user.UserId)
            .ToListAsync();
        Assert.All(tokensAfter, t => Assert.True(t.IsRevoked, $"Token {t.Id} should be revoked"));
        Assert.Contains(tokensAfter, t => t.RevokedReason == "reuse_detected");
        Assert.Contains(tokensAfter, t => t.RevokedReason == "rotation");
        Assert.True(tokensAfter.Count >= 2);
    }

    [Fact]
    public async Task ValidateAsync_ReusedToken_AuditFails_StillRevokesAndSendsOthers()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser(email: "user@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);
        var (plain, token) = await sut.CreateAsync(
            user.UserId.ToString(), null, null, default);
        token.ReplacedByToken = "x";
        token.IsRevoked = true;
        await ctx.SaveChangesAsync();
        ctx.Entry(token).State = EntityState.Detached;

        _audit.Setup(a => a.WriteAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit DB down"));

        // Act — не должно бросить исключение наружу
        var result = await sut.ValidateAsync(plain, user.UserId.ToString(), default);

        // Assert
        Assert.Null(result);

        // Push и email всё равно были вызваны
        _webPush.Verify(p => p.SendNotificationAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendAsync(
            "user@example.com", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_ReusedToken_PushFails_StillSendsEmail()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser(email: "user@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);
        var (plain, token) = await sut.CreateAsync(
            user.UserId.ToString(), null, null, default);
        token.ReplacedByToken = "x";
        token.IsRevoked = true;
        await ctx.SaveChangesAsync();
        ctx.Entry(token).State = EntityState.Detached;

        _webPush.Setup(p => p.SendNotificationAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("push gateway down"));

        // Act
        var result = await sut.ValidateAsync(plain, user.UserId.ToString(), default);

        // Assert
        Assert.Null(result);
        _email.Verify(e => e.SendAsync(
            "user@example.com", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_ReusedToken_UserHasNoEmail_EmailNotSent()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser(email: "");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);
        var (plain, token) = await sut.CreateAsync(
            user.UserId.ToString(), null, null, default);
        token.ReplacedByToken = "x";
        token.IsRevoked = true;
        await ctx.SaveChangesAsync();
        ctx.Entry(token).State = EntityState.Detached;

        // Act
        var result = await sut.ValidateAsync(plain, user.UserId.ToString(), default);

        // Assert
        Assert.Null(result);
        _audit.Verify(a => a.WriteAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _webPush.Verify(p => p.SendNotificationAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ───────────────────────────────────────────────────────────────────
    // RotateAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RotateAsync_RevokesOldAndCreatesNew()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);
        var (_, oldToken) = await sut.CreateAsync(
            user.UserId.ToString(), null, null, default);

        // Act
        var newPlain = await sut.RotateAsync(
            oldToken, user.UserId.ToString(), "192.168.1.1", "TestAgent", default);

        // Assert
        Assert.NotEmpty(newPlain);

        var refreshedOld = await ctx.RefreshTokens.FirstAsync(t => t.Id == oldToken.Id);
        Assert.True(refreshedOld.IsRevoked);
        Assert.Equal("rotation", refreshedOld.RevokedReason);
        Assert.NotNull(refreshedOld.RevokedAt);
        Assert.Equal(64, refreshedOld.ReplacedByToken!.Length);

        var allTokens = await ctx.RefreshTokens.Where(t => t.UserId == user.UserId).ToListAsync();
        Assert.Equal(2, allTokens.Count);
        var newEntity = allTokens.First(t => t.Id != oldToken.Id);
        Assert.False(newEntity.IsRevoked);
        Assert.Null(newEntity.ReplacedByToken);
    }

    // ───────────────────────────────────────────────────────────────────
    // RevokeAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAsync_KnownActiveToken_RevokesWithReason()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);
        var (plain, _) = await sut.CreateAsync(
            user.UserId.ToString(), null, null, default);

        // Act
        await sut.RevokeAsync(plain, user.UserId.ToString(), "logout", default);

        // Assert
        var token = await ctx.RefreshTokens.FirstAsync(t => t.UserId == user.UserId);
        Assert.True(token.IsRevoked);
        Assert.Equal("logout", token.RevokedReason);
        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public async Task RevokeAsync_AlreadyRevoked_IsIdempotent()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);
        var (plain, entity) = await sut.CreateAsync(
            user.UserId.ToString(), null, null, default);
        entity.IsRevoked = true;
        entity.RevokedReason = "first_logout";
        entity.RevokedAt = DateTime.UtcNow.AddMinutes(-5);
        await ctx.SaveChangesAsync();
        var firstRevokedAt = entity.RevokedAt;

        // Act
        await sut.RevokeAsync(plain, user.UserId.ToString(), "second_logout", default);

        // Assert — причина и время не должны перезаписаться
        var token = await ctx.RefreshTokens.FirstAsync(t => t.UserId == user.UserId);
        Assert.Equal("first_logout", token.RevokedReason);
        Assert.Equal(firstRevokedAt, token.RevokedAt);
    }

    [Fact]
    public async Task RevokeAsync_UnknownToken_IsNoOp()
    {
        // Arrange
        await using var ctx = CreateContext();
        var sut = CreateSut(ctx);

        // Act — не должно бросить
        await sut.RevokeAsync("never-issued", Guid.NewGuid().ToString(), default);

        // Assert — БД пустая
        Assert.Empty(await ctx.RefreshTokens.ToListAsync());
    }

    // ───────────────────────────────────────────────────────────────────
    // RevokeAllForUserAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAllForUserAsync_OnlyRevokesActiveTokens()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);

        // Создаём 3 токена; один уже отзываем
        await sut.CreateAsync(user.UserId.ToString(), null, null, default);
        var (plain2, token2) = await sut.CreateAsync(user.UserId.ToString(), null, null, default);
        var (_, token3) = await sut.CreateAsync(user.UserId.ToString(), null, null, default);
        await sut.RevokeAsync(plain2, user.UserId.ToString(), "logout", default);

        // Act
        await sut.RevokeAllForUserAsync(user.UserId.ToString(), "password_change", default);

        // Assert
        var tokens = await ctx.RefreshTokens.Where(t => t.UserId == user.UserId).ToListAsync();
        Assert.Equal(3, tokens.Count);
        Assert.Equal(2, tokens.Count(t => t.IsRevoked && t.RevokedReason == "password_change"));
        Assert.Single(tokens, t => t.RevokedReason == "logout"); // первый logout не перезаписан
    }

    [Fact]
    public async Task RevokeAllForUserAsync_NoActiveTokens_IsNoOp()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);

        // Act + Assert — не бросает
        await sut.RevokeAllForUserAsync(user.UserId.ToString(), default);
        Assert.Empty(await ctx.RefreshTokens.ToListAsync());
    }

    // ───────────────────────────────────────────────────────────────────
    // PurgeExpiredAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PurgeExpiredAsync_DeletesOldRevokedAndOldExpiredTokens()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);

        // 1) старый отозванный (60 дней назад) — должен удалиться
        var (_, oldRevoked) = await sut.CreateAsync(user.UserId.ToString(), null, null, default);
        oldRevoked.IsRevoked = true;
        oldRevoked.RevokedAt = DateTime.UtcNow.AddDays(-60);

        // 2) старый истёкший (60 дней назад) — должен удалиться
        var (_, oldExpired) = await sut.CreateAsync(user.UserId.ToString(), null, null, default);
        oldExpired.ExpiresAt = DateTime.UtcNow.AddDays(-60);

        // 3) недавний отозванный (5 дней назад) — должен остаться
        var (_, recentRevoked) = await sut.CreateAsync(user.UserId.ToString(), null, null, default);
        recentRevoked.IsRevoked = true;
        recentRevoked.RevokedAt = DateTime.UtcNow.AddDays(-5);

        // 4) активный — должен остаться
        var (_, active) = await sut.CreateAsync(user.UserId.ToString(), null, null, default);
        await ctx.SaveChangesAsync();

        // Act
        var deleted = await sut.PurgeExpiredAsync(
            olderThan: DateTime.UtcNow.AddDays(-30), default);

        // Assert
        Assert.Equal(2, deleted);
        var remaining = await ctx.RefreshTokens.Where(t => t.UserId == user.UserId).ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.Contains(remaining, t => t.Id == recentRevoked.Id);
        Assert.Contains(remaining, t => t.Id == active.Id);
    }

    [Fact]
    public async Task PurgeExpiredAsync_DefaultCutoff_Is30Days()
    {
        // Arrange
        await using var ctx = CreateContext();
        var user = CreateUser();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var sut = CreateSut(ctx);

        // 31-дневный отозванный → должен удалиться при default cutoff
        var (_, oldRevoked) = await sut.CreateAsync(user.UserId.ToString(), null, null, default);
        oldRevoked.IsRevoked = true;
        oldRevoked.RevokedAt = DateTime.UtcNow.AddDays(-31);
        await ctx.SaveChangesAsync();

        // Act
        var deleted = await sut.PurgeExpiredAsync(olderThan: null, default);

        // Assert
        Assert.Equal(1, deleted);
    }
}
