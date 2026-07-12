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

        return new AiSafetyDecision
        {
            Result = AiSafetyResult.Allowed,
            Urgency = context.Current.Measurement?.Value > context.Profile.TargetRangeMax ? "MEDIUM" : "LOW"
        };
    }

    [GeneratedRegex("(увеличь|уменьши|измени|поменяй|добавь|введи|уколи|поставь).{0,60}(доз|инсулин|ед\\.?|единиц)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeDosePattern();
}
