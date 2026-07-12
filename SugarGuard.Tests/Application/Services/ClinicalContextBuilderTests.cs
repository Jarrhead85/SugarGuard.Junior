using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SugarGuard.API.Application.Ai;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Tests.Application.Services;

public sealed class ClinicalContextBuilderTests
{
    [Fact]
    public async Task BuildAsync_UsesOnlyRequestedChildClinicalData()
    {
        var factory = new TestAppDbContextFactory(Guid.NewGuid().ToString("N"));
        await using (var db = await factory.CreateDbContextAsync())
        {
            var child = CreateChild("child@example.test");
            var otherChild = CreateChild("other@example.test");
            db.Children.AddRange(child, otherChild);
            db.DiabetesSettings.Add(new DiabetesSettings
            {
                ChildId = child.ChildId,
                TargetRangeMin = 4.0m,
                TargetRangeMax = 10.0m,
                InsulinSensitivity = 2.5m,
                CarbInsulinRatio = 10m
            });
            db.Measurements.AddRange(
                new Measurement
                {
                    ChildId = child.ChildId,
                    GlucoseValue = 6.1m,
                    MeasurementTime = DateTime.UtcNow.AddMinutes(-20),
                    DataSource = "mobile"
                },
                new Measurement
                {
                    ChildId = otherChild.ChildId,
                    GlucoseValue = 19.9m,
                    MeasurementTime = DateTime.UtcNow.AddMinutes(-10),
                    DataSource = "mobile"
                });
            db.NutritionEntries.AddRange(
                new NutritionEntry
                {
                    ChildId = child.ChildId,
                    RecordedAt = DateTime.UtcNow.AddMinutes(-30),
                    MealType = MealType.Snack,
                    MealName = "Яблоко",
                    BreadUnits = 1.0m,
                    InsulinUnits = 0.5m,
                    Source = NutritionEntrySource.Child,
                    CreatedByUserId = Guid.NewGuid()
                },
                new NutritionEntry
                {
                    ChildId = otherChild.ChildId,
                    RecordedAt = DateTime.UtcNow.AddMinutes(-25),
                    MealType = MealType.Snack,
                    MealName = "Чужой перекус",
                    BreadUnits = 5.0m,
                    InsulinUnits = 4.0m,
                    Source = NutritionEntrySource.Child,
                    CreatedByUserId = Guid.NewGuid()
                });
            await db.SaveChangesAsync();
        }

        var builder = new ClinicalContextBuilder(
            factory,
            Options.Create(new AiClinicalContextOptions()));

        var context = await builder.BuildAsync(
            (await GetFirstChildAsync(factory)).ChildId,
            null,
            null,
            "Что делать?",
            CancellationToken.None);

        Assert.Equal(1.0m, context.DailySummary.TotalBreadUnits);
        Assert.Equal(0.5m, context.DailySummary.TotalInsulinUnits);
        Assert.DoesNotContain(context.RecentHistory.Nutrition, item => item.MealName == "Чужой перекус");
        Assert.DoesNotContain("example.test", System.Text.Json.JsonSerializer.Serialize(context));
    }

    [Fact]
    public void SafetyPolicy_BlocksDoseChangingAdvice()
    {
        var policy = new AiRecommendationSafetyPolicy();
        var context = new ClinicalContext
        {
            Current = new CurrentSituationContext
            {
                Measurement = new GlucoseContext
                {
                    Value = 6.2m,
                    MeasuredAt = DateTime.UtcNow,
                    Source = "test"
                }
            },
            Profile = new ClinicalProfileContext
            {
                TargetRangeMin = 4.0m,
                TargetRangeMax = 10.0m
            }
        };

        var result = policy.EvaluateAfterModel(context, "Увеличь дозу инсулина на 2 единицы.");

        Assert.Equal(AiSafetyResult.BlockedUnsafeOutput, result.Result);
        Assert.False(result.CanCallModel);
        Assert.Contains("не могу", result.SafeText);
    }

    private static Child CreateChild(string marker) => new()
    {
        ChildId = Guid.NewGuid(),
        FirstName = marker,
        LastName = "Test",
        DateOfBirth = new DateOnly(2014, 1, 1),
        DiabetesType = "Type1",
        TimeZoneId = "Europe/Moscow"
    };

    private static async Task<Child> GetFirstChildAsync(TestAppDbContextFactory factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Children.OrderBy(child => child.FirstName).FirstAsync();
    }
}
