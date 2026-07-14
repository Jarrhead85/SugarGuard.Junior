using System.Text.RegularExpressions;
using SugarGuard.API.Application.Ai;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Политика безопасности для AI-рекомендаций.
/// </summary>
public sealed partial class AiRecommendationSafetyPolicy : IAiRecommendationSafetyPolicy
{
    /// <inheritdoc/>
    public AiSafetyDecision EvaluateBeforeModel(ClinicalContext context)
    {
        var glucose = context.Current.Measurement?.Value;
        if (!glucose.HasValue)
        {
            return new AiSafetyDecision();
        }

        if (glucose.Value <= 3.1m)
        {
            return new AiSafetyDecision
            {
                Result = AiSafetyResult.LocalFallback,
                Urgency = "CRITICAL",
                CanCallModel = false,
                SafeText = "Глюкоза критически низкая. Немедленно позови взрослого, действуй по утверждённому плану гипогликемии и повтори измерение через 10–15 минут."
            };
        }

        if (glucose.Value < context.Profile.TargetRangeMin)
        {
            return new AiSafetyDecision
            {
                Result = AiSafetyResult.LocalFallback,
                Urgency = "HIGH",
                CanCallModel = false,
                SafeText = "Глюкоза ниже цели. Позови взрослого, используй быстрые углеводы по своему плану и повтори измерение через 10–15 минут."
            };
        }

        if (glucose.Value > 15.0m)
        {
            return new AiSafetyDecision
            {
                Result = AiSafetyResult.LocalFallback,
                Urgency = "CRITICAL",
                CanCallModel = false,
                SafeText = "Глюкоза критически высокая. Сразу сообщи взрослому, пей воду, проверь кетоны по плану и обратись за медицинской помощью при тошноте, рвоте или сильной слабости."
            };
        }

        if (glucose.Value >= 14.0m)
        {
            return new AiSafetyDecision
            {
                Result = AiSafetyResult.LocalFallback,
                Urgency = "HIGH",
                CanCallModel = false,
                SafeText = "Глюкоза высокая. Сообщи взрослому, пей воду и следуй утверждённому плану. Не меняй дозу инсулина без взрослого или врача."
            };
        }

        return new AiSafetyDecision
        {
            Urgency = glucose.Value > context.Profile.TargetRangeMax ? "MEDIUM" : "LOW"
        };
    }

    /// <inheritdoc/>
    public AiSafetyDecision EvaluateAfterModel(ClinicalContext context, string modelResponse)
    {
        if (UnsafeDosePattern().IsMatch(modelResponse))
        {
            return new AiSafetyDecision
            {
                Result = AiSafetyResult.BlockedUnsafeOutput,
                Urgency = "MEDIUM",
                CanCallModel = false,
                SafeText = "Я не могу подсказывать изменение дозы инсулина. Покажи измерение взрослому и действуй только по утверждённому врачом плану."
            };
        }

        if (ContainsUnavailableBackpackFoodAdvice(context, modelResponse))
        {
            return new AiSafetyDecision
            {
                Result = AiSafetyResult.BlockedUnsafeOutput,
                Urgency = context.Current.Measurement?.Value > context.Profile.TargetRangeMax ? "MEDIUM" : "LOW",
                CanCallModel = false,
                SafeText = BuildBackpackGroundedSafeText(context)
            };
        }

        return new AiSafetyDecision
        {
            Result = AiSafetyResult.Allowed,
            Urgency = context.Current.Measurement?.Value > context.Profile.TargetRangeMax ? "MEDIUM" : "LOW"
        };
    }

    private static bool ContainsUnavailableBackpackFoodAdvice(ClinicalContext context, string modelResponse)
    {
        if (string.IsNullOrWhiteSpace(modelResponse) || !SuggestsEatingPattern().IsMatch(modelResponse))
        {
            return false;
        }

        var normalizedResponse = Normalize(modelResponse);
        var availableSnackNames = context.AvailableBackpack
            .Select(item => Normalize(item.SnackName))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        var mentionsBackpack = normalizedResponse.Contains("рюкзак", StringComparison.Ordinal);
        if (mentionsBackpack && !availableSnackNames.Any(name => normalizedResponse.Contains(name, StringComparison.Ordinal)))
        {
            return true;
        }

        return KnownSnackMarkers
            .Where(marker => normalizedResponse.Contains(marker, StringComparison.Ordinal))
            .Any(marker => !availableSnackNames.Any(name => name.Contains(marker, StringComparison.Ordinal)));
    }

    private static string BuildBackpackGroundedSafeText(ClinicalContext context)
    {
        var glucose = context.Current.Measurement?.Value;
        var availableBackpack = FormatAvailableBackpack(context);

        if (glucose.HasValue && glucose.Value < context.Profile.TargetRangeMin)
        {
            return string.IsNullOrWhiteSpace(availableBackpack)
                ? "Глюкоза ниже цели. Позови взрослого: в рюкзаке подходящего перекуса не вижу, используй быстрые углеводы только по своему плану."
                : $"Глюкоза ниже цели. Позови взрослого. В рюкзаке сейчас отмечено: {availableBackpack}. Используй только подходящий быстрый углевод по своему плану и повтори измерение через 10–15 минут.";
        }

        if (glucose.HasValue && glucose.Value > context.Profile.TargetRangeMax)
        {
            return "Глюкоза выше цели. Дополнительные углеводы сейчас не нужны. Сообщи взрослому, пей воду и действуй только по утверждённому плану.";
        }

        return string.IsNullOrWhiteSpace(availableBackpack)
            ? "Глюкоза сейчас около цели. Специально есть сладкое не нужно; наблюдай самочувствие и скажи взрослому, если станет плохо."
            : $"Глюкоза сейчас около цели. Специально есть сладкое не нужно; если ты правда голоден, выбирай только из того, что отмечено в рюкзаке: {availableBackpack}.";
    }

    private static string FormatAvailableBackpack(ClinicalContext context)
    {
        if (context.AvailableBackpack.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "; ",
            context.AvailableBackpack
                .GroupBy(item => new { item.SnackName, item.BreadUnits })
                .OrderBy(group => group.Key.SnackName)
                .ThenBy(group => group.Key.BreadUnits)
                .Select(group => group.Count() == 1
                    ? $"{group.Key.SnackName} ({group.Key.BreadUnits:0.##} ХЕ)"
                    : $"{group.Key.SnackName}: {group.Count()} шт. по {group.Key.BreadUnits:0.##} ХЕ"));
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace('ё', 'е');

    private static readonly string[] KnownSnackMarkers =
    [
        "батончик",
        "банан",
        "булоч",
        "йогурт",
        "конфет",
        "мармелад",
        "печень",
        "сок",
        "сыр",
        "хлебц",
        "шоколад",
        "яблок"
    ];

    [GeneratedRegex("(увеличь|уменьши|измени|поменяй|добавь|введи|уколи|поставь).{0,60}(доз|инсулин|ед\\.?|единиц)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeDosePattern();

    [GeneratedRegex("(съешь|съесть|перекуси|перекус|выпей|возьми|используй|быстрые\\s+углеводы|углевод)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SuggestsEatingPattern();
}
