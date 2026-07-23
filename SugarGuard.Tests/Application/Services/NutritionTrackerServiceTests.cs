using Microsoft.EntityFrameworkCore;
using Moq;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;
using System.Text;

namespace SugarGuard.Tests.Application.Services;

public sealed class NutritionTrackerServiceTests
{
    [Fact]
    public async Task CreateEntryAndSummary_UsesActualBreadUnitsAndInsulin()
    {
        await using var context = CreateContext();
        var child = CreateChild();
        context.Children.Add(child);
        await context.SaveChangesAsync();
        var userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.Setup(service => service.GetRole()).Returns(UserRole.Parent);
        var service = new NutritionTrackerService(context, currentUser.Object);

        var recordedAt = DateTime.UtcNow;
        var created = await service.CreateEntryAsync(child.ChildId, userId, new SaveNutritionEntryRequest
        {
            RecordedAt = recordedAt,
            MealType = MealType.Breakfast,
            MealName = "Каша",
            BreadUnits = 2.5m,
            InsulinUnits = 1.5m,
            GlucoseBefore = 5.8m
        }, CancellationToken.None);

        var summary = await service.GetSummaryAsync(child.ChildId, recordedAt.AddDays(-1), recordedAt.AddDays(1), CancellationToken.None);
        Assert.Equal(NutritionEntrySource.Parent, created.Source);
        Assert.Equal(2.5m, summary.TotalBreadUnits);
        Assert.Equal(1.5m, summary.TotalInsulinUnits);
        Assert.Equal(2.5m, summary.AverageBreadUnitsPerDay);
        Assert.Equal(1.5m, summary.AverageInsulinUnitsPerDay);
        Assert.Equal(3, summary.Days.Count);
        var dayWithEntry = Assert.Single(summary.Days, day => day.EntriesCount > 0);
        Assert.Equal(2.5m, dayWithEntry.BreadUnits);
        Assert.Equal(1.5m, dayWithEntry.InsulinUnits);
    }

    [Fact]
    public async Task Achievements_UnlocksFirstStepsAfterThreeEntries()
    {
        await using var context = CreateContext();
        var child = CreateChild();
        context.Children.Add(child);
        for (var index = 0; index < 3; index++)
        {
            context.NutritionEntries.Add(new NutritionEntry
            {
                ChildId = child.ChildId,
                RecordedAt = DateTime.UtcNow.AddHours(-index),
                MealType = MealType.Snack,
                MealName = "Перекус",
                BreadUnits = 1,
                CreatedByUserId = Guid.NewGuid()
            });
        }
        await context.SaveChangesAsync();
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.Setup(service => service.GetRole()).Returns(UserRole.ChildDevice);
        var service = new NutritionTrackerService(context, currentUser.Object);

        var achievements = await service.GetAchievementsAsync(child.ChildId, CancellationToken.None);

        var firstSteps = Assert.Single(achievements, achievement => achievement.Code == "first_steps");
        Assert.True(firstSteps.IsUnlocked);
        Assert.Equal(3, firstSteps.Progress);
    }

    [Fact]
    public async Task ExportCsvAsync_UsesWindows1251AndPreservesRussianTextForExcel()
    {
        await using var context = CreateContext();
        var child = CreateChild();
        context.Children.Add(child);
        context.NutritionEntries.Add(new NutritionEntry
        {
            ChildId = child.ChildId,
            RecordedAt = DateTime.UtcNow,
            MealType = MealType.Dinner,
            MealName = "Гречка с овощами",
            BreadUnits = 3.5m,
            InsulinUnits = 2m,
            GlucoseBefore = 5.9m,
            CreatedByUserId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.Setup(service => service.GetRole()).Returns(UserRole.Parent);
        var service = new NutritionTrackerService(context, currentUser.Object);

        var bytes = await service.ExportCsvAsync(
            child.ChildId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(1),
            CancellationToken.None);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(1251);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.False(bytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()));

        var csv = encoding.GetString(bytes);
        Assert.StartsWith("sep=;\r\n", csv, StringComparison.Ordinal);
        Assert.Contains("Приём пищи", csv, StringComparison.Ordinal);
        Assert.Contains("Гречка с овощами", csv, StringComparison.Ordinal);
        Assert.Contains("\"3,5\";\"2\";\"5,9\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportPdfAsync_ReturnsPdfForEntriesFromDifferentDays()
    {
        await using var context = CreateContext();
        var child = CreateChild();
        context.Children.Add(child);
        context.NutritionEntries.AddRange(
            new NutritionEntry
            {
                ChildId = child.ChildId,
                RecordedAt = DateTime.UtcNow.AddDays(-1),
                MealType = MealType.Breakfast,
                MealName = "Каша",
                BreadUnits = 2m,
                CreatedByUserId = Guid.NewGuid()
            },
            new NutritionEntry
            {
                ChildId = child.ChildId,
                RecordedAt = DateTime.UtcNow,
                MealType = MealType.Snack,
                MealName = "Йогурт",
                BreadUnits = 1m,
                CreatedByUserId = Guid.NewGuid()
            });
        await context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.Setup(service => service.GetRole()).Returns(UserRole.Parent);
        var service = new NutritionTrackerService(context, currentUser.Object);

        var pdf = await service.ExportPdfAsync(
            child.ChildId,
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow.AddDays(1),
            CancellationToken.None);

        Assert.True(pdf.AsSpan().StartsWith("%PDF"u8));
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static Child CreateChild() => new()
    {
        ChildId = Guid.NewGuid(),
        FirstName = "Тимофей",
        LastName = "Тестов",
        DateOfBirth = new DateOnly(2015, 1, 1),
        DiabetesType = "Type1"
    };
}
