using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;
using System.Globalization;
using System.Text;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация экспорта данных
/// </summary>
public sealed class ExportJobService : IExportJobService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ExportJobService> _logger;

    // Максимальный лимит строк
    private const int MaxListLimit = 500;

    /// <summary>
    /// Конструктор с DI
    /// </summary>
    public ExportJobService(AppDbContext db, ILogger<ExportJobService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExportJobResponse> CreateExportJobAsync(
        CreateExportJobRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Создание задания экспорта: userId={UserId}, childId={ChildId}, from={From}, to={To}, format={Format}.",
            requestedByUserId, request.ChildId, request.PeriodFrom, request.PeriodTo, request.Format);

        // Создаём запись в БД
        var job = new ExportJob
        {
            ExportJobId = Guid.NewGuid(),
            RequestedByUserId = requestedByUserId,
            ChildId = request.ChildId,
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            Format = request.Format ?? "csv",
            Status = "queued",
            CreatedAt = DateTime.UtcNow
        };

        _db.ExportJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        // Переводим в processing
        job.Status = "processing";
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            // Загружаем измерения за период
            var measurements = await _db.Measurements
                .AsNoTracking()
                .Where(m => m.ChildId == request.ChildId
                         && m.MeasurementTime >= request.PeriodFrom
                         && m.MeasurementTime <= request.PeriodTo)
                .OrderBy(m => m.MeasurementTime)
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "Экспорт job={JobId}: загружено {Count} измерений.",
                job.ExportJobId, measurements.Count);

            var csvBytes = BuildCsv(measurements);

            // downloadUrl — относительный путь, по которому контроллер отдаст файл
            var downloadUrl = $"/api/export-jobs/{job.ExportJobId}/download";

            job.Status = "completed";
            job.DownloadUrl = downloadUrl;
            job.CompletedAt = DateTime.UtcNow;

            // Сохраняем CSV-контент в отдельную колонку или внешнее хранилище
            job.CsvContent = csvBytes;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Задание экспорта job={JobId} завершено успешно ({Bytes} байт).",
                job.ExportJobId, csvBytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ошибка генерации экспорта job={JobId}.", job.ExportJobId);

            job.Status = "failed";
            job.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return MapToResponse(job);
    }

    /// <inheritdoc/>
    public async Task<List<ExportJobResponse>> GetExportJobsAsync(
        Guid? childId,
        Guid requestedByUserId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Пользователь видит только свои задания
        var safeLimit = Math.Clamp(limit, 1, MaxListLimit);

        var query = _db.ExportJobs
            .AsNoTracking()
            .Where(j => j.RequestedByUserId == requestedByUserId);

        if (childId.HasValue)
            query = query.Where(j => j.ChildId == childId.Value);

        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "GetExportJobs: userId={UserId}, childId={ChildId}, limit={Limit} → {Count} заданий.",
            requestedByUserId, childId, safeLimit, jobs.Count);

        return jobs.Select(MapToResponse).ToList();
    }

    /// <inheritdoc/>
    public async Task<(byte[] FileBytes, string FileName)?> DownloadExportFileAsync(
        Guid exportJobId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.ExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.ExportJobId == exportJobId, cancellationToken);

        if (job is null)
        {
            _logger.LogWarning(
                "DownloadExportFile: задание job={JobId} не найдено.", exportJobId);
            return null;
        }

        // Проверка владения
        if (job.RequestedByUserId != requestedByUserId)
        {
            _logger.LogWarning(
                "DownloadExportFile: userId={UserId} не является владельцем job={JobId}.",
                requestedByUserId, exportJobId);
            return null;
        }

        if (job.Status != "completed" || job.CsvContent is null || job.CsvContent.Length == 0)
        {
            _logger.LogWarning(
                "DownloadExportFile: job={JobId} не завершён или файл пуст (status={Status}).",
                exportJobId, job.Status);
            return null;
        }

        var fileName = $"sugarguard_export_{job.PeriodFrom:yyyyMMdd}_{job.PeriodTo:yyyyMMdd}.csv";

        _logger.LogInformation(
            "DownloadExportFile: выдаём файл {FileName} ({Bytes} байт) для job={JobId}.",
            fileName, job.CsvContent.Length, exportJobId);

        return (job.CsvContent, fileName);
    }

    // Приватные вспомогательные методы
    /// <summary>
    /// Строит CSV-файл из списка измерений
    /// </summary>
    private static byte[] BuildCsv(List<Measurement> measurements)
    {
        var sb = new StringBuilder();

        // Заголовок
        sb.AppendLine("MeasurementId,MeasurementTime,GlucoseValue,ChildState,DataSource,Notes");

        foreach (var m in measurements)
        {
            // Экранируем поля, которые могут содержать запятые или кавычки
            sb.Append(m.MeasurementId).Append(',');
            sb.Append(m.MeasurementTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(m.GlucoseValue.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsvField(m.ChildState)).Append(',');
            sb.Append(EscapeCsvField(m.DataSource)).Append(',');
            sb.AppendLine(EscapeCsvField(m.Notes));
        }

        // UTF-8 для корректного открытия в Excel
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    /// <summary>
    /// Экранирует поле CSV
    /// </summary>
    private static string EscapeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Если содержит спецсимволы — оборачиваем в кавычки, внутренние кавычки удваиваем
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }

    private static ExportJobResponse MapToResponse(ExportJob job) => new()
    {
        ExportJobId = job.ExportJobId,
        RequestedByUserId = job.RequestedByUserId,
        ChildId = job.ChildId,
        PeriodFrom = job.PeriodFrom,
        PeriodTo = job.PeriodTo,
        Format = job.Format,
        Status = job.Status,
        DownloadUrl = job.DownloadUrl,
        CreatedAt = job.CreatedAt,
        CompletedAt = job.CompletedAt
    };
}
