using Hangfire;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Data;
using SugarGuard.Domain.Entities;
using SugarGuard.Application.Audit;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Infrastructure.BackgroundServices;

/// <summary>
/// Фоновый сервис периодической постобработки журнала синхронизации
/// </summary>

public sealed class SyncBackgroundService : BackgroundService
{
    // Настройки    
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30); // Интервал между циклами polling-обхода

    private const int PendingBatchSize = 100; // Максимальное число записей, обрабатываемых за один цикл

    private const int MaxRetryAttempts = 3; // Максимальное число попыток повтора для записи в статусе

    private const int ConflictBatchSize = 200; // Максимальное число конфликтных записей, разрешаемых за один цикл

    // DI
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncBackgroundService> _logger;

    /// <summary>
    /// Инициализирует фоновый сервис синхронизации
    /// </summary>
    public SyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // BackgroundService
    /// <summary>
    /// Точка входа сервиса
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SyncBackgroundService: запущен. PollingInterval={Interval}s.",
            PollingInterval.TotalSeconds);

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingItemsAsync(stoppingToken);
                await ResolveConflictsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SyncBackgroundService: необработанная ошибка в основном цикле.");
            }

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("SyncBackgroundService: остановлен.");
    }

    // Основные операции
    /// <summary>
    /// Обрабатывает батч записей в статусе
    /// </summary>
    private async Task ProcessPendingItemsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var pendingItems = await db.SyncLogs
            .AsNoTracking()
            .Where(s => s.Status == SyncLogStatus.Pending && !s.IsConflict)
            .OrderBy(s => s.CreatedAt)
            .Take(PendingBatchSize)
            .ToListAsync(cancellationToken);

        if (pendingItems.Count == 0)
            return;

        _logger.LogInformation(
            "SyncBackgroundService: найдено {Count} pending-записей для обработки.",
            pendingItems.Count);

        var processedCount = 0;
        var failedCount = 0;

        foreach (var item in pendingItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

            var log = await db.SyncLogs
                .FromSqlRaw(
                    @"SELECT * FROM sync_logs
                      WHERE sync_log_id = {0}
                        AND status = 'pending'
                        AND NOT is_conflict
                      FOR UPDATE SKIP LOCKED",
                    item.SyncLogId)
                .AsTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (log is null)
            {
                await tx.RollbackAsync(cancellationToken);
                continue;
            }

            try
            {
                var success = await TryApplySyncItemAsync(db, log, cancellationToken);

                if (success)
                {
                    log.Status = "success";
                    log.Error = null;
                    processedCount++;
                }
                else
                {
                    var attempts = ExtractAttemptCount(log.Error) + 1;

                    if (attempts >= MaxRetryAttempts)
                    {
                        log.Status = SyncLogStatus.Failed;
                        log.Error = BuildErrorMeta(
                            $"Превышено максимальное число попыток ({MaxRetryAttempts}).",
                            attempts);
                        failedCount++;

                        _logger.LogWarning(
                            "SyncBackgroundService: запись {SyncLogId} переведена в failed " +
                            "после {Attempts} попыток. EntityType={EntityType} EntityId={EntityId}.",
                            log.SyncLogId, attempts, log.EntityType, log.EntityId);
                    }
                    else
                    {
                        log.Error = BuildErrorMeta("Повторная попытка.", attempts);
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SyncBackgroundService: ошибка при обработке SyncLogId={SyncLogId}.",
                    log.SyncLogId);

                await tx.RollbackAsync(cancellationToken);

                try
                {
                    var attempts = ExtractAttemptCount(log.Error) + 1;
                    log.Status = attempts >= MaxRetryAttempts
                        ? SyncLogStatus.Failed
                        : SyncLogStatus.Pending;
                    log.Error = BuildErrorMeta(ex.Message, attempts);
                    await db.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx,
                        "SyncBackgroundService: не удалось сохранить статус ошибки " +
                        "для SyncLogId={SyncLogId}.", log.SyncLogId);
                }
            }
        }

        if (processedCount > 0 || failedCount > 0)
        {
            _logger.LogInformation(
                "SyncBackgroundService: обработано pending={Processed}, failed={Failed}.",
                processedCount, failedCount);

            await audit.WriteAsync(
                action: "sync.pending.processed",
                targetType: "SyncLog",
                targetId: null,
                details: $"Processed={processedCount} Failed={failedCount}",
                cancellationToken: CancellationToken.None);
        }
    }

    /// <summary>
    /// Разрешает накопившиеся конфликты синхронизации
    /// </summary>
    private async Task ResolveConflictsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var candidateIds = await db.SyncLogs
            .AsNoTracking()
            .Where(s => s.IsConflict && s.Status == SyncLogStatus.Conflict)
            .OrderBy(s => s.CreatedAt)
            .Take(ConflictBatchSize)
            .Select(s => s.SyncLogId)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
            return;

        _logger.LogInformation(
            "SyncBackgroundService: найдено {Count} конфликтных записей для разрешения.",
            candidateIds.Count);

        var resolvedCount = 0;

        foreach (var candidateId in candidateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowsUpdated = await db.SyncLogs
                .Where(s => s.SyncLogId == candidateId
                         && s.IsConflict
                         && s.Status == SyncLogStatus.Conflict)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(s => s.Status, SyncLogStatus.Resolved)
                        .SetProperty(s => s.ResolutionSource, SyncLog.ResolutionSourceServerAuto)
                        .SetProperty(
                            s => s.Error,
                            s => s.Error == null
                                ? $"{SyncResolutionStrategy.ServerWinsOnDuplicate}: дублирующая запись отклонена."
                                : s.Error + $" | Resolved: {SyncResolutionStrategy.ServerWinsOnDuplicate}."),
                    cancellationToken);

            if (rowsUpdated == 1)
            {
                resolvedCount++;
            }
            else
            {
                _logger.LogDebug(
                    "SyncBackgroundService: SyncLogId={SyncLogId} не обновлён " +
                    "(rowsUpdated={RowsUpdated}). Возможно, разрешён параллельным воркером.",
                    candidateId, rowsUpdated);
            }
        }

        if (resolvedCount > 0)
        {
            _logger.LogInformation(
                "SyncBackgroundService: разрешено конфликтов={Resolved}.",
                resolvedCount);

            await audit.WriteAsync(
                action: "sync.conflicts.resolved",
                targetType: "SyncLog",
                targetId: null,
                details: $"Resolved={resolvedCount} Strategy={SyncResolutionStrategy.ServerWinsOnDuplicate}",
                cancellationToken: CancellationToken.None);
        }
    }

    /// <summary>
    /// Пытается применить одну запись журнала синхронизации
    /// </summary>
    private async Task<bool> TryApplySyncItemAsync(
        AppDbContext db,
        SyncLog log,
        CancellationToken cancellationToken)
    {
        switch (log.EntityType)
        {
            case "Measurement":
                {
                    // Проверяем, что измерение уже существует в БД
                    if (!Guid.TryParse(log.EntityId, out var measurementId))
                    {
                        _logger.LogWarning(
                            "SyncBackgroundService: EntityId='{EntityId}' не является валидным Guid " +
                            "для EntityType=Measurement. SyncLogId={SyncLogId}.",
                            log.EntityId, log.SyncLogId);
                        return false;
                    }

                    var exists = await db.Measurements
                        .AnyAsync(m => m.MeasurementId == measurementId, cancellationToken);

                    if (exists)
                    {
                        // Измерение уже в БД
                        _logger.LogDebug(
                            "SyncBackgroundService: Measurement {MeasurementId} уже существует. " +
                            "SyncLogId={SyncLogId} → success.",
                            measurementId, log.SyncLogId);
                        return true;
                    }

                    // Измерение не найдено
                    _logger.LogDebug(
                        "SyncBackgroundService: Measurement {MeasurementId} ещё не найдено в БД. " +
                        "SyncLogId={SyncLogId} остаётся pending.",
                        measurementId, log.SyncLogId);
                    return false;
                }

            default:
                {
                    // Неизвестный тип сущности
                    _logger.LogWarning(
                        "SyncBackgroundService: неизвестный EntityType='{EntityType}'. " +
                        "SyncLogId={SyncLogId}. Переводим в failed.",
                        log.EntityType, log.SyncLogId);
                    return false;
                }
        }
    }

    // Hangfire: агрегированный отчёт
    /// <summary>
    /// Регистрирует Hangfire-задачу агрегации статистики конфликтов
    /// </summary>
    public static void ScheduleRecurringJobs()
    {
        RecurringJob.AddOrUpdate<SyncBackgroundService>(
            recurringJobId: "sync-conflict-report",
            methodCall: svc => svc.GenerateConflictReportAsync(CancellationToken.None),
            cronExpression: Cron.Hourly(),
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    /// <summary>
    /// Собирает агрегированную статистику конфликтов за последний час и пишет её в журнал.
    /// </summary>
    [AutomaticRetry(Attempts = 1)]
    public async Task GenerateConflictReportAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var since = DateTime.UtcNow.AddHours(-1);

        var stats = await db.SyncLogs
            .AsNoTracking()
            .Where(s => s.CreatedAt >= since)
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalConflicts = await db.SyncLogs
            .AsNoTracking()
            .CountAsync(s => s.IsConflict && s.Status == SyncLogStatus.Conflict, cancellationToken);

        var details = string.Join(
            " ",
            stats.Select(s => $"{s.Status}={s.Count}"));

        details += $" UnresolvedConflicts={totalConflicts}";

        _logger.LogInformation(
            "SyncBackgroundService: hourly report. {Details}.", details);

        await audit.WriteAsync(
            action: "sync.hourly.report",
            targetType: "SyncLog",
            targetId: null,
            details: details,
            cancellationToken: CancellationToken.None);
    }

    // Вспомогательные методы
    /// <summary>
    /// Извлекает счётчик попыток
    /// </summary>
    private static int ExtractAttemptCount(string? errorField)
    {
        if (string.IsNullOrWhiteSpace(errorField))
            return 0;

        const string prefix = "attempts=";
        var start = errorField.IndexOf(prefix, StringComparison.Ordinal);

        if (start < 0)
            return 0;

        start += prefix.Length;
        var end = errorField.IndexOf(' ', start);

        var raw = end < 0
            ? errorField[start..]
            : errorField[start..end];

        return int.TryParse(raw, out var n) ? n : 0;
    }

    /// <summary>
    /// Формирует строку метаданных
    /// </summary>
    private static string BuildErrorMeta(string message, int attempts)
        => $"attempts={attempts} | {message}";
}
