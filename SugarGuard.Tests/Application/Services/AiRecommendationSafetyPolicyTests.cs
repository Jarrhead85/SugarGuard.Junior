using SugarGuard.API.Application.Ai;
using SugarGuard.API.Application.Services;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Tests.Application.Services;

public sealed class AiRecommendationSafetyPolicyTests
{
    [Fact]
    public void EvaluateAfterModel_WhenModelSuggestsUnavailableBackpackFood_BlocksResponse()
    {
        var policy = new AiRecommendationSafetyPolicy();
        var context = CreateContextWithBackpack("яблоко", 1.0m);

        var result = policy.EvaluateAfterModel(
            context,
            "Лучше съесть что-то из твоего рюкзака, например булочку или шоколадку.");

        Assert.Equal(AiSafetyResult.BlockedUnsafeOutput, result.Result);
        Assert.False(result.CanCallModel);
        Assert.Contains("яблоко", result.SafeText);
        Assert.DoesNotContain("булоч", result.SafeText);
        Assert.DoesNotContain("шоколад", result.SafeText);
    }

    [Fact]
    public void EvaluateAfterModel_WhenModelSuggestsAvailableBackpackFood_AllowsResponse()
    {
        var policy = new AiRecommendationSafetyPolicy();
        var context = CreateContextWithBackpack("яблоко", 1.0m);

        var result = policy.EvaluateAfterModel(
            context,
            "Если ты правда голоден, можешь взять яблоко из рюкзака и сказать взрослому.");

        Assert.Equal(AiSafetyResult.Allowed, result.Result);
    }

    [Fact]
    public void EvaluateAfterModel_WhenBackpackFoodIsNotMentioned_DoesNotBlockGeneralObservation()
    {
        var policy = new AiRecommendationSafetyPolicy();
        var context = CreateContextWithBackpack("яблоко", 1.0m);

        var result = policy.EvaluateAfterModel(
            context,
            "Глюкоза около цели. Специально есть сладкое не нужно, просто наблюдай самочувствие.");

        Assert.Equal(AiSafetyResult.Allowed, result.Result);
    }

    private static ClinicalContext CreateContextWithBackpack(string snackName, decimal breadUnits) => new()
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
                Value = 7.3m,
                MeasuredAt = DateTime.UtcNow,
                Source = "test"
            }
        },
        AvailableBackpack =
        [
            new BackpackSnackContext
            {
                SnackName = snackName,
                BreadUnits = breadUnits,
                RecordedAt = DateTime.UtcNow
            }
        ]
    };
}
