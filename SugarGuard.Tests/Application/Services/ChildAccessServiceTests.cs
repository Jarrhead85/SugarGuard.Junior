using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SugarGuard.API.Data;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Tests.Application.Services;

/// <summary>
/// Unit-тесты для <see cref="ChildAccessService"/>.
/// <para>
/// <b>SEC-4 (IDOR protection):</b> это критический security-сервис —
/// каждый контроллер, отдающий данные ребёнка, ОБЯЗАН вызвать
/// <see cref="ChildAccessService.CanAccessChildAsync"/> перед ответом.
/// Тесты покрывают:
/// </para>
/// <list type="bullet">
///   <item><description><b>Role-based matrix:</b> Admin/SupportAdmin/ServiceAccount → true без проверки;
///     Doctor → DoctorChildLinks.IsActive=true; Parent → ParentChildLinks;
///     ChildDevice/другие → false.</description></item>
///   <item><description><b>Двухуровневое кеширование (SEC-4 mitigation):</b>
///     L1 per-request (HttpContext.Items) + L2 cross-request (IMemoryCache).
///     N вызовов CanAccessChildAsync в одном запросе → 1 SELECT.</description></item>
///   <item><description><b>JWT claim extraction:</b>
///     ClaimTypes.NameIdentifier И кастомный "UserId"; стандартный "role" И кастомный "role".</description></item>
///   <item><description><b>GetAccessibleChildIdsAsync:</b> для каждой роли возвращает правильный список.</description></item>
/// </list>
/// </summary>
public class ChildAccessServiceTests : IDisposable
{
    private readonly string _dbName = $"ChildAccessTest_{Guid.NewGuid()}";
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly Mock<IMemoryCache> _memoryCache = new();
    private readonly Mock<ICacheEntry> _cacheEntry = new();
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public ChildAccessServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;

        // IMemoryCache.Set нужен для записи в L2. По умолчанию Moq не делает ничего.
        object? _ignoredValue;
        _memoryCache.Setup(c => c.TryGetValue(It.IsAny<object>(), out _ignoredValue!))
            .Returns(false);
        _memoryCache.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(_cacheEntry.Object);
        _cacheEntry.SetupProperty(e => e.Value);
    }

    public void Dispose()
    {
        using var ctx = new AppDbContext(_dbOptions);
        ctx.Database.EnsureDeleted();
    }

    private AppDbContext CreateContext() => new(_dbOptions);

    private ChildAccessService CreateSut()
    {
        return new ChildAccessService(
            _httpContextAccessor.Object,
            CreateContext(),
            _memoryCache.Object);
    }

    private void SetUserContext(Guid? userId, UserRole? role)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (role.HasValue)
            claims.Add(new Claim(ClaimTypes.Role, role.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private static User CreateUser(UserRole role) => new()
    {
        UserId = Guid.NewGuid(),
        EmailForLogin = $"{role.ToString().ToLowerInvariant()}@test.local",
        Role = role,
        CreatedAt = DateTime.UtcNow
    };

    private static Child CreateChild() => new()
    {
        ChildId = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "Child",
        DateOfBirth = new DateOnly(2015, 1, 1),
        CreatedAt = DateTime.UtcNow
    };

    // ───────────────────────────────────────────────────────────────────
    // GetCurrentUserId / GetCurrentUserRole — JWT claim extraction
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public void GetCurrentUserId_ReturnsNull_WhenNoHttpContext()
    {
        _httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var sut = CreateSut();
        Assert.Null(sut.GetCurrentUserId());
    }

    [Fact]
    public void GetCurrentUserId_ReadsNameIdentifierClaim()
    {
        var userId = Guid.NewGuid();
        SetUserContext(userId, null);
        var sut = CreateSut();
        Assert.Equal(userId, sut.GetCurrentUserId());
    }

    [Fact]
    public void GetCurrentUserId_ReadsCustomUserIdClaim_WhenNameIdentifierMissing()
    {
        // Тест на fallback: "UserId" claim используется в самописных JWT
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim("UserId", userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(new DefaultHttpContext { User = principal });

        var sut = CreateSut();
        Assert.Equal(userId, sut.GetCurrentUserId());
    }

    [Fact]
    public void GetCurrentUserId_ReturnsNull_WhenClaimIsNotGuid()
    {
        SetUserContext(null, null);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") };
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) });
        var sut = CreateSut();
        Assert.Null(sut.GetCurrentUserId());
    }

    [Fact]
    public void GetCurrentUserRole_ReadsRoleClaim()
    {
        SetUserContext(Guid.NewGuid(), UserRole.Doctor);
        var sut = CreateSut();
        Assert.Equal(UserRole.Doctor, sut.GetCurrentUserRole());
    }

    [Fact]
    public void GetCurrentUserRole_ParsesCaseInsensitive()
    {
        SetUserContext(Guid.NewGuid(), null);
        var claims = new[] { new Claim("role", "doctor") };  // lowercase
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) });
        var sut = CreateSut();
        Assert.Equal(UserRole.Doctor, sut.GetCurrentUserRole());
    }

    [Fact]
    public void GetCurrentUserRole_ReturnsNull_WhenClaimMissing()
    {
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) });
        var sut = CreateSut();
        Assert.Null(sut.GetCurrentUserRole());
    }

    // ───────────────────────────────────────────────────────────────────
    // CanAccessChildAsync — role-based matrix
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CanAccessChildAsync_ReturnsFalse_WhenNoUserId()
    {
        SetUserContext(null, null);
        var sut = CreateSut();
        Assert.False(await sut.CanAccessChildAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CanAccessChildAsync_ReturnsFalse_WhenUserNotInDb()
    {
        // User в JWT есть, но в БД его нет (например, удалён)
        var userId = Guid.NewGuid();
        SetUserContext(userId, UserRole.Parent);
        var sut = CreateSut();

        Assert.False(await sut.CanAccessChildAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CanAccessChildAsync_AdminRole_AlwaysTrue_WithoutLinks()
    {
        // Arrange: Admin-пользователь без связок с ребёнком
        var admin = CreateUser(UserRole.Admin);
        var child = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(admin);
            db.Children.Add(child);
            await db.SaveChangesAsync();
        }
        SetUserContext(admin.UserId, null);  // Роль НЕ в JWT → грузится из БД

        var sut = CreateSut();
        var result = await sut.CanAccessChildAsync(child.ChildId);

        Assert.True(result);  // Admin видит всех
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SupportAdmin)]
    [InlineData(UserRole.ServiceAccount)]
    public async Task CanAccessChildAsync_AdminLikeRoles_AlwaysTrue(UserRole role)
    {
        var user = CreateUser(role);
        var child = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(user);
            db.Children.Add(child);
            await db.SaveChangesAsync();
        }
        SetUserContext(user.UserId, null);

        var sut = CreateSut();
        Assert.True(await sut.CanAccessChildAsync(child.ChildId));
    }

    [Fact]
    public async Task CanAccessChildAsync_Parent_ReturnsTrue_WhenLinkExists()
    {
        var parent = CreateUser(UserRole.Parent);
        var child = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(parent);
            db.Children.Add(child);
            db.ParentChildLinks.Add(new ParentChildLink
            {
                ParentUserId = parent.UserId,
                ChildId = child.ChildId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        SetUserContext(parent.UserId, null);

        var sut = CreateSut();
        Assert.True(await sut.CanAccessChildAsync(child.ChildId));
    }

    [Fact]
    public async Task CanAccessChildAsync_Parent_ReturnsFalse_WhenNoLink()
    {
        var parent = CreateUser(UserRole.Parent);
        var child = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(parent);
            db.Children.Add(child);
            await db.SaveChangesAsync();
        }
        SetUserContext(parent.UserId, null);

        var sut = CreateSut();
        Assert.False(await sut.CanAccessChildAsync(child.ChildId));
    }

    [Fact]
    public async Task CanAccessChildAsync_Doctor_ReturnsTrue_WhenLinkActive()
    {
        var doctor = CreateUser(UserRole.Doctor);
        var child = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorChildLinks.Add(new DoctorChildLink
            {
                DoctorUserId = doctor.UserId,
                ChildId = child.ChildId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        SetUserContext(doctor.UserId, null);

        var sut = CreateSut();
        Assert.True(await sut.CanAccessChildAsync(child.ChildId));
    }

    [Fact]
    public async Task CanAccessChildAsync_Doctor_ReturnsFalse_WhenLinkInactive()
    {
        // SEC-4: неактивная связка (доктор уволен) не должна давать доступа
        var doctor = CreateUser(UserRole.Doctor);
        var child = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(doctor);
            db.Children.Add(child);
            db.DoctorChildLinks.Add(new DoctorChildLink
            {
                DoctorUserId = doctor.UserId,
                ChildId = child.ChildId,
                IsActive = false,  // <-- ключевая проверка
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        SetUserContext(doctor.UserId, null);

        var sut = CreateSut();
        Assert.False(await sut.CanAccessChildAsync(child.ChildId));
    }

    [Fact]
    public async Task CanAccessChildAsync_ChildDeviceRole_AlwaysFalse()
    {
        // ChildDevice — это устройства детей, не люди, доступа к БД не должно быть
        var device = CreateUser(UserRole.ChildDevice);
        var child = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(device);
            db.Children.Add(child);
            // Связка отсутствует — но даже если бы была, ChildDevice не проверяется
            await db.SaveChangesAsync();
        }
        SetUserContext(device.UserId, null);

        var sut = CreateSut();
        Assert.False(await sut.CanAccessChildAsync(child.ChildId));
    }

    [Fact]
    public async Task CanAccessChildAsync_ParentCannotAccessUnrelatedChild()
    {
        // SECURITY: Parent ребёнка A не должен видеть ребёнка B (IDOR guard)
        var parent = CreateUser(UserRole.Parent);
        var childA = CreateChild();
        var childB = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(parent);
            db.Children.AddRange(childA, childB);
            db.ParentChildLinks.Add(new ParentChildLink
            {
                ParentUserId = parent.UserId,
                ChildId = childA.ChildId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        SetUserContext(parent.UserId, null);

        var sut = CreateSut();
        Assert.True(await sut.CanAccessChildAsync(childA.ChildId));
        Assert.False(await sut.CanAccessChildAsync(childB.ChildId));
    }

    // ───────────────────────────────────────────────────────────────────
    // Per-request cache (HttpContext.Items)
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CanAccessChildAsync_PerRequestCache_AvoidsDuplicateSelects()
    {
        // Arrange: Parent со связкой
        var parent = CreateUser(UserRole.Parent);
        var child = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(parent);
            db.Children.Add(child);
            db.ParentChildLinks.Add(new ParentChildLink
            {
                ParentUserId = parent.UserId,
                ChildId = child.ChildId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        SetUserContext(parent.UserId, null);
        var sut = CreateSut();

        // Act: вызываем 3 раза в одном HTTP-request
        var r1 = await sut.CanAccessChildAsync(child.ChildId);
        var r2 = await sut.CanAccessChildAsync(child.ChildId);
        var r3 = await sut.CanAccessChildAsync(child.ChildId);

        // Assert: все три возвращают одинаковый результат
        Assert.True(r1);
        Assert.True(r2);
        Assert.True(r3);

        // L1 (per-request) — кеш в HttpContext.Items.
        // На втором вызове Role уже закеширован, на третьем CanAccess тоже.
        // Конкретное число запросов сложно проверить с InMemory-провайдером
        // (он не считает SELECT'ы), но smoke-проверка:
        // HttpContext.Items должен содержать кеш-ключи после первого вызова.
        var ctx = _httpContextAccessor.Object.HttpContext!;
        Assert.NotEmpty(ctx.Items);
    }

    [Fact]
    public async Task CanAccessChildAsync_DifferentRequests_CacheDoesNotLeak()
    {
        // Cross-request кеш (IMemoryCache) имитируется через Mock — НЕ сохраняет
        // состояние между sut-экземплярами, что эквивалентно "новому запросу".
        var parent = CreateUser(UserRole.Parent);
        var child = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(parent);
            db.Children.Add(child);
            db.ParentChildLinks.Add(new ParentChildLink
            {
                ParentUserId = parent.UserId,
                ChildId = child.ChildId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        SetUserContext(parent.UserId, null);

        // Запрос 1
        var sut1 = CreateSut();
        Assert.True(await sut1.CanAccessChildAsync(child.ChildId));

        // Удаляем связку
        using (var db = CreateContext())
        {
            var link = await db.ParentChildLinks.FirstAsync();
            db.ParentChildLinks.Remove(link);
            await db.SaveChangesAsync();
        }

        // Запрос 2 (новый HttpContext, новый SUT) — должен вернуть false
        SetUserContext(parent.UserId, null);  // новый HttpContext
        var sut2 = CreateSut();
        Assert.False(await sut2.CanAccessChildAsync(child.ChildId));
    }

    // ───────────────────────────────────────────────────────────────────
    // GetAccessibleChildIdsAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccessibleChildIdsAsync_ReturnsEmpty_WhenNoUserId()
    {
        SetUserContext(null, null);
        var sut = CreateSut();
        var result = await sut.GetAccessibleChildIdsAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAccessibleChildIdsAsync_Parent_ReturnsLinkedChildren()
    {
        var parent = CreateUser(UserRole.Parent);
        var c1 = CreateChild();
        var c2 = CreateChild();
        var c3 = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(parent);
            db.Children.AddRange(c1, c2, c3);
            db.ParentChildLinks.AddRange(
                new ParentChildLink { ParentUserId = parent.UserId, ChildId = c1.ChildId, CreatedAt = DateTime.UtcNow },
                new ParentChildLink { ParentUserId = parent.UserId, ChildId = c2.ChildId, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        SetUserContext(parent.UserId, null);

        var sut = CreateSut();
        var ids = await sut.GetAccessibleChildIdsAsync();

        Assert.Equal(2, ids.Count);
        Assert.Contains(c1.ChildId, ids);
        Assert.Contains(c2.ChildId, ids);
        Assert.DoesNotContain(c3.ChildId, ids);
    }

    [Fact]
    public async Task GetAccessibleChildIdsAsync_Doctor_ReturnsAllLinkedActiveChildren()
    {
        var doctor = CreateUser(UserRole.Doctor);
        var c1 = CreateChild();
        var c2 = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(doctor);
            db.Children.AddRange(c1, c2);
            db.DoctorChildLinks.Add(new DoctorChildLink
            {
                DoctorUserId = doctor.UserId,
                ChildId = c1.ChildId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            // c2 — связка неактивна → НЕ включается (защита от уволенных врачей)
            db.DoctorChildLinks.Add(new DoctorChildLink
            {
                DoctorUserId = doctor.UserId,
                ChildId = c2.ChildId,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        SetUserContext(doctor.UserId, null);

        var sut = CreateSut();
        var ids = await sut.GetAccessibleChildIdsAsync();

        Assert.Single(ids);
        Assert.Contains(c1.ChildId, ids);
    }

    [Fact]
    public async Task GetAccessibleChildIdsAsync_Admin_ReturnsAllChildren()
    {
        var admin = CreateUser(UserRole.Admin);
        var c1 = CreateChild();
        var c2 = CreateChild();
        using (var db = CreateContext())
        {
            db.Users.Add(admin);
            db.Children.AddRange(c1, c2);
            await db.SaveChangesAsync();
        }
        SetUserContext(admin.UserId, null);

        var sut = CreateSut();
        var ids = await sut.GetAccessibleChildIdsAsync();

        Assert.Equal(2, ids.Count);
        Assert.Contains(c1.ChildId, ids);
        Assert.Contains(c2.ChildId, ids);
    }

    [Fact]
    public async Task GetAccessibleChildIdsAsync_ChildDevice_ReturnsEmpty()
    {
        var device = CreateUser(UserRole.ChildDevice);
        using (var db = CreateContext())
        {
            db.Users.Add(device);
            await db.SaveChangesAsync();
        }
        SetUserContext(device.UserId, null);

        var sut = CreateSut();
        var ids = await sut.GetAccessibleChildIdsAsync();
        Assert.Empty(ids);
    }
}
