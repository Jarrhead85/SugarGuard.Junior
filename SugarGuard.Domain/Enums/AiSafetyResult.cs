namespace SugarGuard.Domain.Enums;

/// <summary>
/// Результат проверки безопасности AI-рекомендации.
/// </summary>
public enum AiSafetyResult
{
    /// <summary>
    /// Ответ разрешён политикой безопасности.
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// Использован локальный безопасный сценарий без доверия внешней модели.
    /// </summary>
    LocalFallback = 1,

    /// <summary>
    /// Ответ модели был заблокирован и заменён безопасным текстом.
    /// </summary>
    BlockedUnsafeOutput = 2
}
