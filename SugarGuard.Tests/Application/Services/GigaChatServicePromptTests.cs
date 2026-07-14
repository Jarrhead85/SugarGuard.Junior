using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SugarGuard.API.Application.Ai;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Application.Services;

namespace SugarGuard.Tests.Application.Services;

public sealed class GigaChatServicePromptTests
{
    [Fact]
    public void BuildPrompt_WhenStructuredContextProvided_IncludesClinicalDigestInsteadOfRawJsonOnly()
    {
        var now = DateTime.UtcNow;
        var context = new ClinicalContext
        {
            Profile = new ClinicalProfileContext
            {
                TargetRangeMin = 4.0m,
                TargetRangeMax = 8.0m
            },
            Current = new CurrentSituationContext
            {
                Measurement = new GlucoseContext
                {
                    MeasuredAt = now,
                    Value = 7.4m,
                    Source = "manual"
                },
                LastMeal = new NutritionContext
                {
                    RecordedAt = now.AddMinutes(-45),
                    MealType = "Snack",
                    MealName = "йогурт",
                    BreadUnits = 1.2m,
                    Source = "mobile"
                },
                LastInsulin = new InsulinContext
                {
                    RecordedAt = now.AddMinutes(-40),
                    Units = 0.5m,
                    MealType = "Snack",
                    Source = "mobile"
                },
                MinutesSinceMeal = 45,
                MinutesSinceInsulin = 40
            },
            DailySummary = new DailyClinicalSummaryContext
            {
                MeasurementCount = 5,
                AverageGlucose = 6.8m,
                MinGlucose = 5.1m,
                MaxGlucose = 8.2m,
                TimeInRangePercent = 80m,
                HighEpisodes = 1,
                TotalBreadUnits = 4.5m,
                TotalInsulinUnits = 1.8m
            },
            AvailableBackpack =
            [
                new BackpackSnackContext
                {
                    SnackName = "сок",
                    BreadUnits = 1.0m,
                    RecordedAt = now.AddHours(-2)
                }
            ]
        };

        var request = new GigaChatRequest
        {
            ChildId = Guid.NewGuid(),
            ChildAge = 10,
            DiabetesType = "1 типа",
            CurrentGlucose = 7.4,
            GlucoseStatus = "НОРМА",
            Trend = "стабильно",
            TargetRangeMin = 4.0,
            TargetRangeMax = 8.0,
            Question = "Что мне сейчас делать?",
            StructuredContextJson = JsonSerializer.Serialize(context, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };

        var prompt = InvokeBuildPrompt(request);

        Assert.Contains("Рюкзак сейчас: сок", prompt);
        Assert.Contains("Последняя еда/перекус: Snack (йогурт), 1.2 ХЕ, 45 мин назад.", prompt);
        Assert.Contains("Последний инсулин: 0.5 ед. (Snack), 40 мин назад.", prompt);
        Assert.Contains("Статистика дня: 5 измер.", prompt);
        Assert.Contains("Не предлагай продукты", prompt);
        Assert.DoesNotContain("\"availableBackpack\"", prompt);
    }

    private static string InvokeBuildPrompt(GigaChatRequest request)
    {
        using var httpClient = new HttpClient();
        var service = new GigaChatService(
            httpClient,
            new ConfigurationBuilder().Build(),
            NullLogger<GigaChatService>.Instance,
            context: null!,
            new GigaChatTokenCache());

        var method = typeof(GigaChatService).GetMethod(
            "BuildPrompt",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(service, [request]));
    }
}
