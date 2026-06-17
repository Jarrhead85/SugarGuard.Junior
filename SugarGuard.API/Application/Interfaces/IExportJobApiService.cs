using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Бизнес-логика API-операций с задачами экспорта CSV
/// </summary>
public interface IExportJobApiService
{
    /// <summary>
    /// Создаёт задачу экспорта со статусом и ставит её в фоновую очередь
    /// </summary>

    Task<ExportJob> CreateAsync(
        CreateExportJobRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список задач экспорта
    /// </summary>
    Task<IReadOnlyList<ExportJobResponse>> GetListAsync(
        Guid? childId,
        Guid requestedByUserId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает задачу по ID или null
    /// </summary>
    Task<ExportJob?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Фиксирует в аудит-логе факт скачивания CSV-файла
    /// </summary>
    Task RecordDownloadedAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
