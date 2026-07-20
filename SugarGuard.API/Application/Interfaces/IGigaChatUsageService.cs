using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Формирует агрегированную статистику использования GigaChat для администратора.
/// </summary>
public interface IGigaChatUsageService
{
    Task<GigaChatUsageResponse> GetAsync(CancellationToken cancellationToken = default);
}
