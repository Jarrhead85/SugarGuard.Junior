using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Tests.Application.Services;

public sealed class TelegramNotificationServiceTests
{
    [Fact]
    public async Task SendMeasurementNotificationAsync_UsesChildTimeZoneInMessage()
    {
        await using var context = CreateContext();
        var child = new Child
        {
            ChildId = Guid.NewGuid(),
            FirstName = "Тимофей",
            LastName = "Петров",
            TimeZoneId = "Europe/Moscow"
        };
        var parent = new User
        {
            UserId = Guid.NewGuid(),
            TelegramId = 123456789
        };

        context.Children.Add(child);
        context.Users.Add(parent);
        context.ParentChildLinks.Add(new ParentChildLink
        {
            LinkId = Guid.NewGuid(),
            ChildId = child.ChildId,
            ParentUserId = parent.UserId,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var outbox = new TelegramOutboxService(context, NullLogger<TelegramOutboxService>.Instance);
        var service = new TelegramNotificationService(
            context,
            outbox,
            NullLogger<TelegramNotificationService>.Instance);

        var result = await service.SendMeasurementNotificationAsync(new MeasurementNotificationRequest
        {
            ChildId = child.ChildId.ToString(),
            GlucoseValue = 6.2,
            Status = "Норма",
            MeasurementTime = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc)
        });

        Assert.True(result.Success);
        var message = await context.TelegramOutboxMessages.SingleAsync();
        Assert.Contains("🕐 Время: 15:00", message.Text, StringComparison.Ordinal);
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
