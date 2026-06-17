using System.Security.Cryptography;
using System.Text;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Бизнес-логика привязки Telegram-пользователя к ребёнку через одноразовый код
/// </summary>
public interface IParentLinkService
{
    /// <summary>
    /// Проверяет существование ребёнка
    /// </summary>
    Task<bool> ChildExistsAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт новый код соединения для ребёнка
    /// </summary>
    Task<SaveConnectionCodeResult> SaveConnectionCodeAsync(
        SaveConnectionCodeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Забирает ConnectionCode через транзакцию
    /// </summary>

    Task<VerifyConnectionCodeResult> VerifyConnectionCodeAsync(
        VerifyConnectionCodeRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат операции сохранения ConnectionCode
/// </summary>
public sealed record SaveConnectionCodeResult(
    bool Success,
    Guid? CodeId,
    DateTime? ExpiresAt,
    string? ErrorMessage);

/// <summary>
/// Результат операции верификации ConnectionCode
/// </summary>
public sealed record VerifyConnectionCodeResult(
    bool Success,
    bool IsValid,
    Guid? ChildId,
    Guid? LinkId,
    string? Message,
    string? ErrorMessage);
