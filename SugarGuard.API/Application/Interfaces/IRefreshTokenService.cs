using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Сервис управления токенами
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Создаёт новый refresh-токен для пользователя
    /// </summary>
    Task<(string PlainToken, RefreshToken Entity)> CreateAsync(
        string userId,
        string? createdByIp,
        string? createdByUserAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Валидирует входящий refresh-токен
    /// </summary>
    Task<RefreshToken?> ValidateAsync(
        string plainToken,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ротирует refresh-токен
    /// </summary>
    Task<string> RotateAsync(
        RefreshToken existingToken,
        string userId,
        string? createdByIp,
        string? createdByUserAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отзывает конкретный refresh-токен
    /// </summary>
    Task RevokeAsync(
        string plainToken,
        string userId,
        string reason = "logout",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отзывает все активные refresh-токены пользователя
    /// </summary>
    Task RevokeAllForUserAsync(
        string userId,
        string reason = "revoke_all",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет устаревшие токены из БД
    /// </summary>
    Task<int> PurgeExpiredAsync(
        DateTime? olderThan = null,
        CancellationToken cancellationToken = default);
}
