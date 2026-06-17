namespace SugarGuard.Web.ViewModels;

/// <summary>Запись журнала синхронизации для UI.</summary>
public sealed class SyncLogVm
{
    public Guid SyncLogId { get; init; }
    public Guid ChildId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsConflict { get; init; }
    public string? Error { get; init; }
    public DateTime CreatedAt { get; init; }
}
