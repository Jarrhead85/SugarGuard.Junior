using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.Domain.Entities;
using SugarGuard.Application.Audit;

namespace SugarGuard.API.Infrastructure.Jobs;

/// <summary>
/// Hangfire-задача формирования CSV-выгрузки измерений глюкозы за период
/// </summary>

public sealed class ExportJobProcessor
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ICsvExportService _csv;
    private readonly IAuditService _auditService;
    private readonly ILogger<ExportJobProcessor> _logger;
   
    private const int MaxMeasurementsPerExport = 100_000; // Максимальное число измерений в одной выгрузке

    /// <summary>
    /// Инициализирует процессор экспортных задач
    /// </summary>
    public ExportJobProcessor(
        IDbContextFactory<AppDbContext> dbFactory,
        IWebHostEnvironment environment,
        ICsvExportService csv,
        IAuditService auditService,
        ILogger<ExportJobProcessor> logger)
    {
        _dbFactory = dbFactory;
        _environment = environment;
        _csv = csv;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Выполняет экспорт измерений в CSV-файл.
    /// </summary>
    public async Task ExecuteAsync(Guid exportJobId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ExportJob: начало обработки. ExportJobId={ExportJobId}.", exportJobId);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Загружаем запись задачи из БД
        var job = await db.ExportJobs
            .FirstOrDefaultAsync(j => j.ExportJobId == exportJobId, cancellationToken);

        if (job is null)
        {
            _logger.LogWarning(
                "ExportJob: запись не найдена. ExportJobId={ExportJobId}.", exportJobId);
            return;
        }

        if (job.Status is "completed" or "failed")
        {
            _logger.LogWarning(
                "ExportJob: задача уже завершена со статусом {Status}. " +
                "ExportJobId={ExportJobId}. Пропускаем.",
                job.Status, exportJobId);
            return;
        }

        // Переводим в статус "в обработке"
        await SetStatusAsync(db, job, "processing", cancellationToken);

        try
        {
            // Получаем измерения за запрошенный период
            var measurements = await db.Measurements
                .AsNoTracking()
                .Where(m => m.ChildId == job.ChildId
                         && m.MeasurementTime >= job.PeriodFrom
                         && m.MeasurementTime <= job.PeriodTo)
                .OrderBy(m => m.MeasurementTime)
                .Take(MaxMeasurementsPerExport)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "ExportJob: получено {Count} измерений. ExportJobId={ExportJobId}.",
                measurements.Count, exportJobId);

            // Строим CSV-контент
            var csvContent = _csv.BuildMeasurementsCsv(measurements);

            // Сохраняем файл на диск
            var filePath = await SaveFileAsync(exportJobId, csvContent, cancellationToken);

            _logger.LogInformation(
                "ExportJob: файл сохранён. Path={FilePath} ExportJobId={ExportJobId}.",
                filePath, exportJobId);

            // Формируем URL скачивания
            job.DownloadUrl = $"/api/export-jobs/{exportJobId:N}/download";
            job.Status = "completed";
            job.CompletedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            await _auditService.WriteAsync(
                action: "export.completed",
                targetType: "ExportJob",
                targetId: exportJobId.ToString(),
                details: $"ChildId={job.ChildId} Format={job.Format} " +
                            $"MeasurementsCount={measurements.Count} " +
                            $"PeriodFrom={job.PeriodFrom:O} PeriodTo={job.PeriodTo:O}",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "ExportJob: успешно завершён. ExportJobId={ExportJobId} " +
                "Measurements={Count}.",
                exportJobId, measurements.Count);
        }
        catch (OperationCanceledException)
        {
            // возвращаем в очередь
            _logger.LogWarning(
                "ExportJob: отменён из-за остановки сервера. ExportJobId={ExportJobId}. " +
                "Возврат в статус queued для повтора.",
                exportJobId);

            await SetStatusAsync(db, job, "queued", CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ExportJob: ошибка выполнения. ExportJobId={ExportJobId}.", exportJobId);

            try
            {
                job.Status = "failed";
                job.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);

                await _auditService.WriteAsync(
                    action: "export.failed",
                    targetType: "ExportJob",
                    targetId: exportJobId.ToString(),
                    details: $"Error={ex.Message}",
                    cancellationToken: CancellationToken.None);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "ExportJob: не удалось сохранить статус failed. " +
                    "ExportJobId={ExportJobId}.", exportJobId);
            }

            throw;
        }
    }

    // Вспомогательные методы
    /// <summary>
    /// Сохраняет CSV-контент в файл и возвращает полный путь к нему
    /// </summary>
    private async Task<string> SaveFileAsync(
        Guid exportJobId,
        string csvContent,
        CancellationToken cancellationToken)
    {
        var exportsDirectory = Path.Combine(
            _environment.ContentRootPath, "exports");

        Directory.CreateDirectory(exportsDirectory);

        var fileName = $"export-{exportJobId:N}.csv";
        var filePath = Path.Combine(exportsDirectory, fileName);

        await File.WriteAllTextAsync(filePath, csvContent, System.Text.Encoding.UTF8, cancellationToken);

        return filePath;
    }

    /// <summary>
    /// Обновляет статус задачи в БД и сохраняет изменения
    /// </summary>
    private async Task SetStatusAsync(
        AppDbContext db,
        ExportJob job,
        string status,
        CancellationToken cancellationToken)
    {
        job.Status = status;
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "ExportJob: статус изменён на {Status}. ExportJobId={ExportJobId}.",
            status, job.ExportJobId);
    }
}
