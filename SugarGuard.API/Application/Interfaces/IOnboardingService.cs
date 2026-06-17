using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Dto;
using System.Data.SqlTypes;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Сервис онбординга пользователя
/// </summary>
public interface IOnboardingService
{
    /// <summary>
    /// Возвращает текущий статус для пользователя
    /// </summary>
    Task<OnboardingStatusResponse> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмечает указанный шаг как завершённый
    /// </summary>
    Task<OnboardingStatusResponse> CompleteStepAsync(
        Guid userId,
        int step,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Пропускает весь онбординг целиком
    /// </summary>
    Task SkipAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает общее количество шагов онбординга для указанной роли
    /// </summary>
    int GetTotalSteps(UserRole role);
}
