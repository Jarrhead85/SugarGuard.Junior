using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Сервис управления заданиями экспорта данных (CSV/PDF).
/// Создаёт задания, отдаёт их список и содержимое готового файла.
/// </summary>
public interface IExportJobService
{
    /// <summary>
    /// Создаёт новое задание экспорта и синхронно генерирует CSV-файл
    /// </summary>

    Task<ExportJobResponse> CreateExportJobAsync(
        CreateExportJobRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список заданий экспорта с опциональной фильтрацией по ребёнку
    /// </summary>
    Task<List<ExportJobResponse>> GetExportJobsAsync(
        Guid? childId,
        Guid requestedByUserId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает содержимое готового CSV-файла по ID задания
    /// </summary>
    Task<(byte[] FileBytes, string FileName)?> DownloadExportFileAsync(
        Guid exportJobId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);
}
