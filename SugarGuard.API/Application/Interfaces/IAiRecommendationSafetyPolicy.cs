using SugarGuard.API.Application.Ai;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Проверяет безопасность AI-рекомендаций до и после вызова модели.
/// </summary>
public interface IAiRecommendationSafetyPolicy
{
    /// <summary>
    /// Выполняет проверку до вызова внешней модели.
    /// </summary>
    AiSafetyDecision EvaluateBeforeModel(ClinicalContext context);

    /// <summary>
    /// Проверяет и при необходимости заменяет ответ модели.
    /// </summary>
    AiSafetyDecision EvaluateAfterModel(ClinicalContext context, string modelResponse);
}

/// <summary>
/// Результат проверки безопасности AI-рекомендации.
/// </summary>
public sealed class AiSafetyDecision
{
    /// <summary>
    /// Результат политики безопасности.
    /// </summary>
    public AiSafetyResult Result { get; set; } = AiSafetyResult.Allowed;

    /// <summary>
    /// Уровень срочности.
    /// </summary>
    public string Urgency { get; set; } = "LOW";

    /// <summary>
    /// Безопасный локальный текст, если модель не должна определять действие.
    /// </summary>
    public string? SafeText { get; set; }

    /// <summary>
    /// Признак, что модель можно вызывать.
    /// </summary>
    public bool CanCallModel { get; set; } = true;
}
