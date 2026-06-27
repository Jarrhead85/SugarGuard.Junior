using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Результат удаления перекуса. Позволяет контроллеру различить 404 (item не найден)
/// и 403 (item принадлежит ребёнку, к которому у пользователя нет доступа)
/// </summary>
public enum BackpackRemoveResult
{
    Removed, 
    NotFound,
    Forbidden
}

/// <summary>
/// Бизнес-логика рюкзака перекусов ребёнка
/// </summary>
public interface IBackpackService
{
    /// <summary>
    /// Возвращает содержимое рюкзака ребёнка
    /// </summary>
    Task<BackpackResponse?> GetAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет существование ребёнка
    /// </summary>
    Task<bool> ChildExistsAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет новый перекус в рюкзак
    /// </summary>
    Task<BackpackItemResponse> AddAsync(
        CreateBackpackItemRequest request,
        Guid addedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет название и ХЕ позиции рюкзака.
    /// </summary>
    Task<BackpackUpdateOutcome> UpdateAsync(
        Guid itemId,
        UpdateBackpackItemRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет перекус с предварительным сохранением истории
    /// </summary>
    Task<BackpackRemoveResult> RemoveAsync(
        Guid itemId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает перекус по ID
    /// </summary>
    Task<BackpackItem?> GetByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмечает перекус как съеденный
    /// </summary>
    Task<BackpackConsumeOutcome> ConsumeAsync(
        Guid itemId,
        Guid consumedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат потребления перекуса
/// </summary>
public enum BackpackConsumeResultStatus
{
    Consumed,
    NotFound,
    Forbidden
}

/// <summary>
/// Outcome потребления перекуса
/// </summary>
public sealed record BackpackConsumeOutcome(
    BackpackConsumeResultStatus Status,
    ConsumeSnackResult? Result);

/// <summary>
/// Результат операции потребления перекуса
/// </summary>
public sealed record ConsumeSnackResult(
    Guid ChildId,
    string SnackName,
    decimal BreadUnits,
    DateTime ConsumedAt);

public enum BackpackUpdateResultStatus
{
    Updated,
    NotFound,
    Forbidden
}

public sealed record BackpackUpdateOutcome(
    BackpackUpdateResultStatus Status,
    BackpackItemResponse? Item);
