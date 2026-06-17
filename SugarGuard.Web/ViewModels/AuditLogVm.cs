namespace SugarGuard.Web.ViewModels;

/// <summary>
/// ViewModel записи аудита для административного интерфейса.
/// </summary>
public sealed class AuditLogVm
{
    /// <summary>
    /// Идентификатор записи аудита.
    /// </summary>
    public Guid AuditLogId { get; init; }

    /// <summary>
    /// Идентификатор пользователя, выполнившего действие.
    /// </summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>
    /// Код или имя действия.
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Тип сущности, над которой выполнено действие.
    /// </summary>
    public string? TargetType { get; init; }

    /// <summary>
    /// Идентификатор целевой сущности.
    /// </summary>
    public string? TargetId { get; init; }

    /// <summary>
    /// Дополнительные детали события аудита.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Дата и время создания записи аудита в UTC.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
