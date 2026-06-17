using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Бизнес-логика измерений глюкозы
/// </summary>
public interface IMeasurementsService
{
    /// <summary>
    /// Проверяет существование ребёнка
    /// </summary>
    Task<bool> ChildExistsAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает ребёнка
    /// </summary>
    Task<Child?> GetChildAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт новое измерение и возвращает результат с рассчитанным статусом
    /// </summary>
    Task<Measurement> CreateAsync(
        CreateMeasurementRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список измерений ребёнка за период с лимитом
    /// </summary>
    Task<IReadOnlyList<Measurement>> GetByChildAsync(
        Guid childId,
        DateTime? from,
        DateTime? to,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает последнее измерение ребёнка
    /// </summary>
    Task<Measurement?> GetLatestAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает измерение по его ID
    /// </summary>
    Task<Measurement?> GetByIdAsync(
        Guid measurementId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список измерений за период
    /// </summary>
    Task<IReadOnlyList<Measurement>> GetForStatisticsAsync(
        Guid childId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Синхронизация измерений от MAUI-приложения
    /// </summary>
    Task<SyncMeasurementsResult> SyncBatchAsync(
        SyncMeasurementsRequest request,
        Func<CancellationToken, Task<IReadOnlyList<Guid>>> getAccessibleChildIdsAsync,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат синхронизации измерений
/// </summary>
public sealed record SyncMeasurementsResult(
    int SuccessCount,
    int ErrorCount,
    IReadOnlyList<SyncConflictDto> Conflicts);
