namespace SugarGuard.Web.ViewModels;

/// <summary>
/// История AI-диалога ребёнка без технических данных провайдера.
/// </summary>
public sealed class AiConversationHistoryVm
{
    /// <summary>
    /// Идентификатор активной AI-конверсации.
    /// </summary>
    public Guid? ConversationId { get; init; }

    /// <summary>
    /// Краткое резюме диалога.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Последние сообщения диалога.
    /// </summary>
    public IReadOnlyList<AiConversationMessageVm> Messages { get; init; } = [];
}

/// <summary>
/// Сообщение AI-диалога, доступное пользователю.
/// </summary>
public sealed class AiConversationMessageVm
{
    /// <summary>
    /// Идентификатор сообщения.
    /// </summary>
    public Guid MessageId { get; init; }

    /// <summary>
    /// Роль сообщения.
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Текст сообщения.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Время создания сообщения.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Идентификатор связанного измерения.
    /// </summary>
    public Guid? MeasurementId { get; init; }

    /// <summary>
    /// Идентификатор связанной рекомендации.
    /// </summary>
    public Guid? RecommendationId { get; init; }

    /// <summary>
    /// Модель или локальный механизм, сформировавший ответ.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Результат проверки безопасности.
    /// </summary>
    public string? SafetyResult { get; init; }

    /// <summary>
    /// Признак сообщения пользователя.
    /// </summary>
    public bool IsUser => string.Equals(Role, "User", StringComparison.OrdinalIgnoreCase);
}
