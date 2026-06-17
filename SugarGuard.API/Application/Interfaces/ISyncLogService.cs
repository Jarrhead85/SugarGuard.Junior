using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Бизнес-логика логов синхронизации и разрешения конфликтов MAUI - сервер
/// </summary>
public interface ISyncLogService
{
    /// <summary>
    /// Возвращает список логов с фильтрацией
    /// </summary>
    Task<IReadOnlyList<SyncLogResponse>> GetAsync(
        IReadOnlyCollection<Guid>? childIds,
        bool onlyConflicts,
        int limit,
        DateTime? since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Разрешает один конфликт вручную
    /// </summary>

    Task<(SyncLog? Log, ResolveOneStatus Status)> ResolveAsync(
        Guid id,
        string resolution,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Разрешает все конфликты для указанного набора детей
    /// </summary>
    Task<int> ResolveAllAsync(
        IReadOnlyCollection<Guid> childIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат операции
/// </summary>
public enum ResolveOneStatus
{
    Success,
    NotFound,
    NotAConflict
}
