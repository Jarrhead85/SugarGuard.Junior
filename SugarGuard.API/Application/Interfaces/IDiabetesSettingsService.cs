using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Медицинские настройки диабета
/// </summary>
public interface IDiabetesSettingsService
{
    /// <summary>
    /// Получить настройки диабета для ребёнка
    /// </summary>
    Task<DiabetesSettingsResponse?> GetAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создать настройки диабета для ребёнка
    /// </summary>
    Task<DiabetesSettingsResponse?> UpsertAsync(
        Guid childId,
        UpdateDiabetesSettingsRequest request,
        CancellationToken cancellationToken = default);
}
