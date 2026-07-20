using Microsoft.EntityFrameworkCore;
using Moq;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Tests.Application.Services;

public sealed class UserNotificationServiceTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly Mock<ICurrentUserContext> _currentUser = new();

    public UserNotificationServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UserNotifications_{Guid.NewGuid():N}")
            .Options;
    }

    [Fact]
    public async Task PersistCriticalLocationAsync_CreatesUnreadParentAlertWithCoordinates()
    {
        var child = CreateChild();
        var parent = CreateParent();
        await SeedLinkAsync(parent, child);

        var sut = CreateSut(parent.UserId);
        var measuredAt = new DateTime(2026, 7, 16, 12, 30, 0, DateTimeKind.Utc);

        await sut.PersistCriticalLocationAsync(new CriticalAlertRequest
        {
            ChildId = child.ChildId.ToString(),
            CriticalGlucose = 2.7,
            MeasurementTime = measuredAt,
            Latitude = 44.878783,
            Longitude = 37.327421,
            Address = "Анапа"
        });

        await using var db = NewDb();
        var notification = await db.UserNotifications.SingleAsync();

        Assert.Equal(parent.UserId, notification.RecipientUserId);
        Assert.Equal(child.ChildId, notification.ChildId);
        Assert.Equal("danger", notification.Type);
        Assert.Equal("critical_location", notification.SourceType);
        Assert.Equal(measuredAt, notification.CreatedAt);
        Assert.False(notification.IsRead);
        Assert.Contains("Критический уровень глюкозы", notification.Title);
        Assert.Contains("2", notification.Description);
        Assert.Contains("Координаты: 44.878783, 37.327421", notification.Description);
        Assert.DoesNotContain("http", notification.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersistCriticalLocationAsync_DeduplicatesRecentCriticalAlertsPerParent()
    {
        var child = CreateChild();
        var parent = CreateParent();
        await SeedLinkAsync(parent, child);

        var sut = CreateSut(parent.UserId);
        var request = new CriticalAlertRequest
        {
            ChildId = child.ChildId.ToString(),
            CriticalGlucose = 2.7,
            MeasurementTime = DateTime.UtcNow,
            Latitude = 44.878783,
            Longitude = 37.327421
        };

        await sut.PersistCriticalLocationAsync(request);
        await sut.PersistCriticalLocationAsync(request);

        await using var db = NewDb();
        Assert.Equal(1, await db.UserNotifications.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_KeepsOnlyFiveHundredNewestNotificationsPerRecipient()
    {
        var parent = CreateParent();
        var oldest = DateTime.UtcNow.AddDays(-2);

        await using var db = NewDb();
        db.Users.Add(parent);
        db.UserNotifications.AddRange(Enumerable.Range(0, 501).Select(index => new UserNotification
        {
            RecipientUserId = parent.UserId,
            Type = "info",
            Title = $"Notification {index}",
            Description = "Retention test",
            SourceType = "retention_test",
            SourceId = Guid.NewGuid(),
            CreatedAt = oldest.AddMinutes(index)
        }));

        await db.SaveChangesAsync();

        var notifications = await db.UserNotifications
            .OrderBy(notification => notification.CreatedAt)
            .ToListAsync();

        Assert.Equal(AppDbContext.MaxNotificationsPerRecipient, notifications.Count);
        Assert.Equal(oldest.AddMinutes(1), notifications.First().CreatedAt);
        Assert.DoesNotContain(notifications, notification => notification.CreatedAt == oldest);
    }

    private UserNotificationService CreateSut(Guid currentUserId)
    {
        _currentUser.Reset();
        _currentUser.Setup(context => context.GetUserId()).Returns(currentUserId);
        _currentUser.Setup(context => context.GetRole()).Returns(UserRole.Parent);
        return new UserNotificationService(NewDb(), _currentUser.Object);
    }

    private async Task SeedLinkAsync(User parent, Child child)
    {
        await using var db = NewDb();
        db.Users.Add(parent);
        db.Children.Add(child);
        db.ParentChildLinks.Add(new ParentChildLink
        {
            LinkId = Guid.NewGuid(),
            ParentUserId = parent.UserId,
            ChildId = child.ChildId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private AppDbContext NewDb() => new(_dbOptions);

    private static User CreateParent() => new()
    {
        UserId = Guid.NewGuid(),
        Role = UserRole.Parent,
        EmailForLogin = $"parent-{Guid.NewGuid():N}@test.local",
        CreatedAt = DateTime.UtcNow,
        IsActive = true
    };

    private static Child CreateChild() => new()
    {
        ChildId = Guid.NewGuid(),
        FirstName = "Тест",
        LastName = "Ребёнок",
        DateOfBirth = new DateOnly(2016, 1, 1),
        DiabetesType = "type1",
        Weight = 30,
        Height = 140,
        TimeZoneId = "Europe/Moscow",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public void Dispose()
    {
        using var db = NewDb();
        db.Database.EnsureDeleted();
    }
}
