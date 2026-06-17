namespace SugarGuard.Web.ViewModels;

/// <summary>
/// ViewModel конфликта синхронизации для Web UI.
/// </summary>
public sealed class SyncConflictVm
{
    /// <summary>
    /// Идентификатор конфликтующей сущности.
    /// </summary>
    public string EntityId { get; init; } = string.Empty;

    /// <summary>
    /// Тип конфликтующей сущности.
    /// </summary>
    public string EntityType { get; init; } = string.Empty;

    /// <summary>
    /// Дата и время изменения серверной версии в UTC.
    /// </summary>
    public DateTime ServerModifiedAt { get; init; }

    /// <summary>
    /// Дата и время изменения локальной версии в UTC.
    /// </summary>
    public DateTime LocalModifiedAt { get; init; }

    /// <summary>
    /// Строковое описание серверной версии сущности.
    /// </summary>
    public string? ServerVersion { get; init; }

    /// <summary>
    /// Победившая версия при разрешении конфликта.
    /// </summary>
    public string WinningVersion { get; init; } = string.Empty;

    /// <summary>
    /// Стратегия разрешения конфликта.
    /// </summary>
    public string ResolutionStrategy { get; init; } = string.Empty;
}
