using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.Infrastructure.Jobs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Tests.Infrastructure.Jobs;

public sealed class DailyParentSummaryJobTests
{
    [Fact]
    public async Task ExecuteAsync_AfterMidnight_UsesPreviousMoscowDayAndDoesNotDuplicateSummary()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new AppDbContext(options);

        var parent = new User
        {
            UserId = Guid.NewGuid(),
            EmailForLogin = "parent@example.test",
            IsActive = true,
            DailySummaryEnabled = true
        };
        var child = new Child
        {
            ChildId = Guid.NewGuid(),
            FirstName = "Тимофей",
            LastName = "Петров",
            DateOfBirth = new DateOnly(2014, 1, 1),
            DiabetesType = "Тип 1"
        };
        var dayStartUtc = new DateTime(2026, 7, 20, 21, 0, 0, DateTimeKind.Utc);

        db.Users.Add(parent);
        db.Children.Add(child);
        db.ParentChildLinks.Add(new ParentChildLink
        {
            LinkId = Guid.NewGuid(),
            ParentUserId = parent.UserId,
            ChildId = child.ChildId,
            CreatedAt = dayStartUtc
        });
        db.DiabetesSettings.Add(new DiabetesSettings
        {
            ChildId = child.ChildId,
            TargetRangeMin = 4m,
            TargetRangeMax = 10m
        });
        db.Measurements.AddRange(
            new Measurement { ChildId = child.ChildId, GlucoseValue = 5.6m, MeasurementTime = dayStartUtc.AddHours(5) },
            new Measurement { ChildId = child.ChildId, GlucoseValue = 7.2m, MeasurementTime = dayStartUtc.AddHours(10) });
        db.NutritionEntries.AddRange(
            new NutritionEntry { ChildId = child.ChildId, RecordedAt = dayStartUtc.AddHours(6), BreadUnits = 1m, InsulinUnits = 1m },
            new NutritionEntry { ChildId = child.ChildId, RecordedAt = dayStartUtc.AddHours(11), BreadUnits = 1.5m, InsulinUnits = 2m });
        await db.SaveChangesAsync();

        var emailService = new Mock<IEmailService>();
        var telegramService = new Mock<ITelegramNotificationService>();
        var maxService = new Mock<IMaxBotService>();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 21, 21, 14, 0, TimeSpan.Zero));
        var job = new DailyParentSummaryJob(
            db,
            emailService.Object,
            telegramService.Object,
            maxService.Object,
            NullLogger<DailyParentSummaryJob>.Instance,
            timeProvider);

        await job.ExecuteAsync();
        await job.ExecuteAsync();

        var summary = await db.UserNotifications.SingleAsync();
        Assert.Contains("Глюкоза: 2 изм.", summary.Description);
        Assert.Contains("Питание: 2,5 ХЕ, инсулин 3 ед.", summary.Description);
    }

    [Fact]
    public void ResolveSummaryDay_AfterMidnight_ReturnsPreviousDay()
    {
        var summaryDay = DailyParentSummaryJob.ResolveSummaryDay(new DateTime(2026, 7, 22, 0, 14, 0));

        Assert.Equal(new DateOnly(2026, 7, 21), summaryDay);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
