using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Tests.Application.Services;

/// <summary>
/// Unit-тесты для <see cref="BackpackService"/>.
/// <para>
/// Покрывает:
/// <list type="bullet">
///   <item><description>CRUD: Get/Add/Remove/Consume/GetById</description></item>
///   <item><description>Аудит: каждый mutate-метод пишет событие в IAuditService</description></item>
///   <item><description><b>SEC-7 (IDOR defense-in-depth):</b> Remove/Consume проверяют
///     доступ к ребёнку через IChildAccessService, даже если контроллер уже сделал это</description></item>
///   <item><description>History: удаление и потребление создают BackpackHistory с правильным DeletedBy-маркером</description></item>
///   <item><description>Consume: создаёт SnackConsumptionLog + history + удаляет item</description></item>
/// </list>
/// </para>
/// </summary>
public class BackpackServiceTests : IDisposable
{
    private readonly string _dbName = $"BackpackTest_{Guid.NewGuid()}";
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IChildAccessService> _childAccess = new();

    public void Dispose()
    {
        using var ctx = new TestAppDbContextFactory(_dbName).CreateDbContext();
        ctx.Database.EnsureDeleted();
    }

    private BackpackService CreateSut() => new(
        new TestAppDbContextFactory(_dbName),
        _audit.Object,
        _childAccess.Object,
        NullLogger<BackpackService>.Instance);

    private static Child CreateChild(string firstName = "Test") => new()
    {
        ChildId = Guid.NewGuid(),
        FirstName = firstName,
        LastName = "Child",
        DateOfBirth = new DateOnly(2015, 1, 1),
        CreatedAt = DateTime.UtcNow
    };

    private static async Task SeedChildAsync(BackpackService sut, Child child)
    {
        // Сохраняем напрямую — AddAsync в сервисе не создаёт детей
        using var db = new TestAppDbContextFactory(((TestAppDbContextFactory)
            ((dynamic)sut).GetType().GetField("_dbFactory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(sut)!).ToString()
            ).CreateDbContext();
        // (альтернативный путь: создать ctx через factory напрямую)
    }

    private TestAppDbContextFactory CreateFactory() => new(_dbName);

    // ───────────────────────────────────────────────────────────────────
    // ChildExistsAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChildExistsAsync_ReturnsTrue_ForExistingChild()
    {
        // Arrange
        var child = CreateChild();
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // Act
        var result = await sut.ChildExistsAsync(child.ChildId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ChildExistsAsync_ReturnsFalse_ForMissingChild()
    {
        var sut = CreateSut();
        var result = await sut.ChildExistsAsync(Guid.NewGuid());
        Assert.False(result);
    }

    // ───────────────────────────────────────────────────────────────────
    // GetAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsEmptyResponse_ForChildWithNoItems()
    {
        // Arrange
        var child = CreateChild();
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // Act
        var response = await sut.GetAsync(child.ChildId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(child.ChildId, response!.ChildId);
        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalItems);
        Assert.Equal(0m, response.TotalBreadUnits);
    }

    [Fact]
    public async Task GetAsync_ReturnsItemsWithTotalBreadUnits_OrderedByCreatedAt()
    {
        // Arrange
        var child = CreateChild();
        var item1 = new BackpackItem
        {
            ChildId = child.ChildId,
            SnackName = "Яблоко",
            BreadUnits = 1.0m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var item2 = new BackpackItem
        {
            ChildId = child.ChildId,
            SnackName = "Печенье",
            BreadUnits = 1.5m,
            CreatedAt = DateTime.UtcNow
        };
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            db.BackpackItems.AddRange(item1, item2);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // Act
        var response = await sut.GetAsync(child.ChildId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, response!.TotalItems);
        Assert.Equal(2.5m, response.TotalBreadUnits);
        Assert.Equal("Яблоко", response.Items[0].SnackName);  // oldest first
        Assert.Equal("Печенье", response.Items[1].SnackName);
    }

    [Fact]
    public async Task GetAsync_OnlyReturnsItemsForRequestedChild()
    {
        // Arrange
        var child1 = CreateChild("A");
        var child2 = CreateChild("B");
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.AddRange(child1, child2);
            db.BackpackItems.Add(new BackpackItem { ChildId = child1.ChildId, SnackName = "А", BreadUnits = 1m });
            db.BackpackItems.Add(new BackpackItem { ChildId = child2.ChildId, SnackName = "Б", BreadUnits = 1m });
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // Act
        var response = await sut.GetAsync(child1.ChildId);

        // Assert
        Assert.NotNull(response);
        Assert.Single(response!.Items);
        Assert.Equal("А", response.Items[0].SnackName);
    }

    // ───────────────────────────────────────────────────────────────────
    // AddAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_PersistsItemAndWritesAuditEvent()
    {
        // Arrange
        var child = CreateChild();
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var request = new CreateBackpackItemRequest
        {
            ChildId = child.ChildId,
            SnackName = "  Шоколадка  ",  // with whitespace
            BreadUnits = 2.0m
        };
        var actorId = Guid.NewGuid();

        // Act
        var response = await sut.AddAsync(request, actorId, default);

        // Assert
        Assert.NotEqual(Guid.Empty, response.BackpackItemId);
        Assert.Equal(child.ChildId, response.ChildId);
        Assert.Equal("Шоколадка", response.SnackName);  // trimmed
        Assert.Equal(2.0m, response.BreadUnits);
        Assert.Equal($"userId:{actorId}", response.AddedBy);

        // Audit
        // Тест не зависит от культуры: проверяем подстроки «Шоколадка» и «2»
        // (а не «2.0» — decimal.ToString() использует CurrentCulture).
        _audit.Verify(a => a.WriteAsync(
            "backpack.item_added",
            "BackpackItem",
            response.BackpackItemId.ToString(),
            It.Is<string>(s => s.Contains("Шоколадка") && s.Contains("=2")),
            It.IsAny<CancellationToken>()), Times.Once);

        // Persisted
        using var verifyDb = CreateFactory().CreateDbContext();
        var stored = await verifyDb.BackpackItems.SingleAsync();
        Assert.Equal("Шоколадка", stored.SnackName);
    }

    [Fact]
    public async Task AddAsync_DoesNotCallChildAccess_BecauseControllerAlreadyChecks()
    {
        // AddAsync в текущей реализации НЕ проверяет IChildAccessService —
        // это ответственность контроллера (item просто привязан к ChildId из request).
        // Документируем это явно: тест защитит от регрессии, если кто-то добавит
        // лишнюю проверку.
        var child = CreateChild();
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        await sut.AddAsync(new CreateBackpackItemRequest
        {
            ChildId = child.ChildId,
            SnackName = "X",
            BreadUnits = 1m
        }, Guid.NewGuid(), default);

        _childAccess.Verify(
            c => c.CanAccessChildAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ───────────────────────────────────────────────────────────────────
    // GetByIdAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsItem_WhenExists()
    {
        // Arrange
        var child = CreateChild();
        var item = new BackpackItem
        {
            ChildId = child.ChildId,
            SnackName = "Яблоко",
            BreadUnits = 1m
        };
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            db.BackpackItems.Add(item);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(item.BackpackItemId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(item.BackpackItemId, result!.BackpackItemId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var sut = CreateSut();
        var result = await sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // ───────────────────────────────────────────────────────────────────
    // RemoveAsync — IDOR (SEC-7) + history
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_ReturnsNotFound_ForMissingItem()
    {
        var sut = CreateSut();
        var result = await sut.RemoveAsync(Guid.NewGuid(), Guid.NewGuid(), default);
        Assert.Equal(BackpackRemoveResult.NotFound, result);
    }

    [Fact]
    public async Task RemoveAsync_ReturnsForbidden_WhenChildAccessDenied()
    {
        // Arrange
        var child = CreateChild();
        var item = new BackpackItem { ChildId = child.ChildId, SnackName = "X", BreadUnits = 1m };
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            db.BackpackItems.Add(item);
            await db.SaveChangesAsync();
        }
        var attackerId = Guid.NewGuid();
        _childAccess
            .Setup(c => c.CanAccessChildAsync(child.ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = CreateSut();

        // Act
        var result = await sut.RemoveAsync(item.BackpackItemId, attackerId, default);

        // Assert
        Assert.Equal(BackpackRemoveResult.Forbidden, result);

        // Item не удалён, history не создана
        using var verifyDb = CreateFactory().CreateDbContext();
        Assert.NotNull(await verifyDb.BackpackItems.SingleOrDefaultAsync());
        Assert.Empty(await verifyDb.BackpackHistory.ToListAsync());
        _audit.Verify(a => a.WriteAsync(
            It.Is<string>(s => s.StartsWith("backpack.")),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_DeletesItemCreatesHistoryAndWritesAudit()
    {
        // Arrange
        var child = CreateChild();
        var item = new BackpackItem
        {
            ChildId = child.ChildId,
            SnackName = "Банан",
            BreadUnits = 1.5m
        };
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            db.BackpackItems.Add(item);
            await db.SaveChangesAsync();
        }
        _childAccess
            .Setup(c => c.CanAccessChildAsync(child.ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut();
        var removerId = Guid.NewGuid();

        // Act
        var result = await sut.RemoveAsync(item.BackpackItemId, removerId, default);

        // Assert
        Assert.Equal(BackpackRemoveResult.Removed, result);

        using var verifyDb = CreateFactory().CreateDbContext();
        Assert.Empty(await verifyDb.BackpackItems.ToListAsync());

        var history = await verifyDb.BackpackHistory.SingleAsync();
        Assert.Equal(child.ChildId, history.ChildId);
        Assert.Equal("Банан", history.SnackName);
        Assert.Equal(1.5m, history.BreadUnits);
        Assert.Equal(BackpackHistoryActor.RemovedByUser(removerId), history.DeletedBy);
        Assert.NotNull(history.DeletedAt);

        _audit.Verify(a => a.WriteAsync(
            "backpack.item_removed",
            "BackpackItem",
            item.BackpackItemId.ToString(),
            It.Is<string>(s => s.Contains("Банан")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // ConsumeAsync — IDOR + history + SnackConsumptionLog
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConsumeAsync_ReturnsNotFound_ForMissingItem()
    {
        var sut = CreateSut();
        var result = await sut.ConsumeAsync(Guid.NewGuid(), Guid.NewGuid(), default);
        Assert.Equal(BackpackConsumeResultStatus.NotFound, result.Status);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task ConsumeAsync_ReturnsForbidden_WhenChildAccessDenied()
    {
        // Arrange
        var child = CreateChild();
        var item = new BackpackItem { ChildId = child.ChildId, SnackName = "X", BreadUnits = 1m };
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            db.BackpackItems.Add(item);
            await db.SaveChangesAsync();
        }
        _childAccess
            .Setup(c => c.CanAccessChildAsync(child.ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = CreateSut();

        // Act
        var result = await sut.ConsumeAsync(item.BackpackItemId, Guid.NewGuid(), default);

        // Assert
        Assert.Equal(BackpackConsumeResultStatus.Forbidden, result.Status);
        Assert.Null(result.Result);

        // Ничего не изменилось
        using var verifyDb = CreateFactory().CreateDbContext();
        Assert.NotNull(await verifyDb.BackpackItems.SingleOrDefaultAsync());
        Assert.Empty(await verifyDb.BackpackHistory.ToListAsync());
        Assert.Empty(await verifyDb.SnackConsumptionLogs.ToListAsync());
    }

    [Fact]
    public async Task ConsumeAsync_CreatesLogAndHistoryAndDeletesItem()
    {
        // Arrange
        var child = CreateChild();
        var item = new BackpackItem
        {
            ChildId = child.ChildId,
            SnackName = "Морковка",
            BreadUnits = 0.5m
        };
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            db.BackpackItems.Add(item);
            await db.SaveChangesAsync();
        }
        _childAccess
            .Setup(c => c.CanAccessChildAsync(child.ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut();
        var consumerId = Guid.NewGuid();

        // Act
        var result = await sut.ConsumeAsync(item.BackpackItemId, consumerId, default);

        // Assert
        Assert.Equal(BackpackConsumeResultStatus.Consumed, result.Status);
        Assert.NotNull(result.Result);
        Assert.Equal(child.ChildId, result.Result!.ChildId);
        Assert.Equal("Морковка", result.Result.SnackName);
        Assert.Equal(0.5m, result.Result.BreadUnits);

        // DB: item удалён, log + history созданы
        using var verifyDb = CreateFactory().CreateDbContext();
        Assert.Empty(await verifyDb.BackpackItems.ToListAsync());

        var log = await verifyDb.SnackConsumptionLogs.SingleAsync();
        Assert.Equal(child.ChildId, log.ChildId);
        Assert.Equal("Морковка", log.SnackName);
        Assert.Equal(0.5m, log.BreadUnits);

        var history = await verifyDb.BackpackHistory.SingleAsync();
        Assert.Equal(BackpackHistoryActor.ConsumedByUser(consumerId), history.DeletedBy);

        // Audit
        _audit.Verify(a => a.WriteAsync(
            "backpack.item_consumed",
            "SnackConsumptionLog",
            log.LogId.ToString(),
            It.Is<string>(s => s.Contains("Морковка")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_DeletedBy_DistinguishesConsumeFromManualRemove()
    {
        // Arrange: тот же ребёнок, два item'а — один удаляем вручную, другой потребляем
        var child = CreateChild();
        var itemToRemove = new BackpackItem { ChildId = child.ChildId, SnackName = "R", BreadUnits = 1m };
        var itemToConsume = new BackpackItem { ChildId = child.ChildId, SnackName = "C", BreadUnits = 1m };
        using (var db = CreateFactory().CreateDbContext())
        {
            db.Children.Add(child);
            db.BackpackItems.AddRange(itemToRemove, itemToConsume);
            await db.SaveChangesAsync();
        }
        _childAccess
            .Setup(c => c.CanAccessChildAsync(child.ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut();
        var userId = Guid.NewGuid();

        await sut.RemoveAsync(itemToRemove.BackpackItemId, userId, default);
        await sut.ConsumeAsync(itemToConsume.BackpackItemId, userId, default);

        // Assert: сортируем по SnackName DESC, чтобы R (Remove) шла первой
        using var verifyDb = CreateFactory().CreateDbContext();
        var history = await verifyDb.BackpackHistory.OrderByDescending(h => h.SnackName).ToListAsync();
        Assert.Equal(2, history.Count);
        // history[0] = R (Remove) → "userId:..."
        // history[1] = C (Consume) → "consumed:userId:..."
        Assert.Equal(BackpackHistoryActor.RemovedByUser(userId), history[0].DeletedBy);
        Assert.Equal(BackpackHistoryActor.ConsumedByUser(userId), history[1].DeletedBy);
    }
}
