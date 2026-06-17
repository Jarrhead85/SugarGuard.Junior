using Hangfire;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.Application.Audit;

namespace SugarGuard.API.Infrastructure.Jobs;

/// <summary>
/// Hangfire-задача периодической очистки устаревших данных
/// </summary>
public sealed class CleanupJob
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuditService _auditService;
    private readonly ILogger<CleanupJob> _logger;

    // TTL-константы   
    private static readonly TimeSpan RefreshTokenRevokedRetention = TimeSpan.FromDays(30); // Срок хранения отозванных
   
    private static readonly TimeSpan ExportJobRetention = TimeSpan.FromDays(30); // Срок хранения завершённых/упавших задач

    private static readonly TimeSpan AuditLogRetention = TimeSpan.FromDays(90); // Срок хранения записей аудита

    private static readonly TimeSpan SyncLogRetention = TimeSpan.FromDays(30); // Срок хранения журнала синхронизации
   
    private static readonly TimeSpan InvitationCodeRetention = TimeSpan.FromDays(7); // Срок хранения завершённых кодов приглашений

    /// <summary>
    /// Инициализирует задачу очистки
    /// </summary>
    public CleanupJob(
        AppDbContext db,
        IWebHostEnvironment environment,
        IAuditService auditService,
        ILogger<CleanupJob> logger)
    {
        _db = db;
        _environment = environment;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Выполняет полный цикл очистки устаревших данных
    /// </summary>
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 300, 900 })]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        _logger.LogInformation(
            "CleanupJob: начало суточной очистки. UtcNow={UtcNow:O}.", utcNow);

        // Статистика по каждой секции
        var stats = new CleanupStats();

        stats.RefreshTokensDeleted = await PurgeRefreshTokensAsync(utcNow, cancellationToken);
        stats.ConnectionCodesDeleted = await PurgeConnectionCodesAsync(utcNow, cancellationToken);
        stats.InvitationCodesDeleted = await PurgeInvitationCodesAsync(utcNow, cancellationToken);
        stats.ExportJobsDeleted = await PurgeExportJobsAsync(utcNow, cancellationToken);
        stats.AuditLogsDeleted = await PurgeAuditLogsAsync(utcNow, cancellationToken);
        stats.SyncLogsDeleted = await PurgeSyncLogsAsync(utcNow, cancellationToken);

        _logger.LogInformation(
            "CleanupJob: завершён. RefreshTokens={RefreshTokens} " +
            "ConnectionCodes={ConnectionCodes} " +
            "InvitationCodes={InvitationCodes} ExportJobs={ExportJobs} " +
            "AuditLogs={AuditLogs} SyncLogs={SyncLogs}.",
            stats.RefreshTokensDeleted,
            stats.ConnectionCodesDeleted,
            stats.InvitationCodesDeleted,
            stats.ExportJobsDeleted,
            stats.AuditLogsDeleted,
            stats.SyncLogsDeleted);

        // Записываем итог
        try
        {
            await _auditService.WriteAsync(
                action: "cleanup.daily",
                targetType: null,
                targetId: null,
                details: $"RefreshTokens={stats.RefreshTokensDeleted} " +
                            $"ConnectionCodes={stats.ConnectionCodesDeleted} " +
                            $"InvitationCodes={stats.InvitationCodesDeleted} " +
                            $"ExportJobs={stats.ExportJobsDeleted} " +
                            $"AuditLogs={stats.AuditLogsDeleted} " +
                            $"SyncLogs={stats.SyncLogsDeleted}",
                cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupJob: не удалось записать аудит-событие.");
        }
    }

    // Секции очистки
    /// <summary>
    /// Удаляет RefreshToken-записи, у которых истёк срок действия или токен отозван
    /// </summary>
    private async Task<int> PurgeRefreshTokensAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        try
        {
            var revokedCutoff = utcNow - RefreshTokenRevokedRetention;

            var deleted = await _db.RefreshTokens
                .Where(t => t.ExpiresAt < utcNow
                         || (t.IsRevoked && t.RevokedAt != null && t.RevokedAt < revokedCutoff))
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogDebug(
                "CleanupJob: RefreshTokens удалено {Count}.", deleted);

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupJob: ошибка очистки RefreshTokens.");
            return 0;
        }
    }

    /// <summary>
    /// Удаляет коды, у которых истёк срок 
    /// </summary>
    private async Task<int> PurgeConnectionCodesAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _db.ConnectionCodes
                .Where(c => c.ExpiresAt < utcNow)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogDebug(
                "CleanupJob: ConnectionCodes удалено {Count}.", deleted);

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupJob: ошибка очистки ConnectionCodes.");
            return 0;
        }
    }

    /// <summary>
    /// Удаляет <c>InvitationCodes</c> в финальных статусах
    /// </summary>
    private async Task<int> PurgeInvitationCodesAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = utcNow - InvitationCodeRetention;

            var deleted = await _db.InviteCodes
                .Where(i => i.CreatedAt < cutoff
                         && (i.Status == "Expired"
                          || i.Status == "Claimed"
                          || i.Status == "Rejected"))
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogDebug(
                "CleanupJob: InvitationCodes удалено {Count}.", deleted);

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupJob: ошибка очистки InvitationCodes.");
            return 0;
        }
    }

    private async Task<int> PurgeExportJobsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = utcNow - ExportJobRetention;

            // Сначала загружаем ID и имена файлов
            var staleJobs = await _db.ExportJobs
                .AsNoTracking()
                .Where(j => j.CreatedAt < cutoff
                         && (j.Status == "completed" || j.Status == "failed"))
                .Select(j => j.ExportJobId)
                .ToListAsync(cancellationToken);

            if (staleJobs.Count == 0)
                return 0;

            // Удаляем CSV-файлы с диска
            var exportsDirectory = Path.Combine(_environment.ContentRootPath, "exports");
            var filesDeleted = 0;

            foreach (var jobId in staleJobs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = Path.Combine(exportsDirectory, $"export-{jobId:N}.csv");

                if (!File.Exists(filePath))
                    continue;

                try
                {
                    File.Delete(filePath);
                    filesDeleted++;
                }
                catch (Exception fileEx)
                {
                    _logger.LogWarning(fileEx,
                        "CleanupJob: не удалось удалить файл экспорта. " +
                        "ExportJobId={ExportJobId} Path={Path}.",
                        jobId, filePath);
                }
            }

            // Удаляем записи из БД одним запросом
            var dbDeleted = await _db.ExportJobs
                .Where(j => staleJobs.Contains(j.ExportJobId))
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogDebug(
                "CleanupJob: ExportJobs удалено из БД {DbCount}, " +
                "файлов удалено {FilesCount}.",
                dbDeleted, filesDeleted);

            return dbDeleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupJob: ошибка очистки ExportJobs.");
            return 0;
        }
    }

    /// <summary>
    /// Удаляет логи старше 90 дней
    /// </summary>
    private async Task<int> PurgeAuditLogsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = utcNow - AuditLogRetention;

            var deleted = await _db.AuditLogs
                .Where(a => a.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogDebug(
                "CleanupJob: AuditLogs удалено {Count}.", deleted);

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupJob: ошибка очистки AuditLogs.");
            return 0;
        }
    }

    private async Task<int> PurgeSyncLogsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = utcNow - SyncLogRetention;

            var deleted = await _db.SyncLogs
                .Where(s => s.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogDebug(
                "CleanupJob: SyncLogs удалено {Count}.", deleted);

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupJob: ошибка очистки SyncLogs.");
            return 0;
        }
    }

    // Регистрация расписания
    /// <summary>
    /// Регистрирует рекуррентный Hangfire-джоб
    /// </summary>
    public static void ScheduleRecurringJob()
    {
        RecurringJob.AddOrUpdate<CleanupJob>(
            recurringJobId: "daily-cleanup",
            methodCall: job => job.ExecuteAsync(CancellationToken.None),
            cronExpression: Cron.Daily(hour: 1, minute: 0),   // 01:00 UTC
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    // Вспомогательные типы
    /// <summary>
    /// Накопленная статистика одного запуска очистки
    /// </summary>
    private sealed class CleanupStats
    {       
        public int RefreshTokensDeleted { get; set; } // Удалено устаревших RefreshToken
       
        public int ConnectionCodesDeleted { get; set; } // Удалено просроченных кодов подключения
       
        public int InvitationCodesDeleted { get; set; } // Удалено завершённых кодов приглашений
       
        public int ExportJobsDeleted { get; set; } // Удалено устаревших задач экспорта
       
        public int AuditLogsDeleted { get; set; } // Удалено старых записей аудита
       
        public int SyncLogsDeleted { get; set; } // Удалено старых записей синхронизации
    }
}
