using Hangfire;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;

namespace SugarGuard.API.Infrastructure.Jobs;

/// <summary>
/// Hangfire задача для ежедневной очистки рюкзаков в полночь
/// </summary>
public class MidnightCleanupJob
{
    private readonly BackpackCleanupService _cleanupService;
    private readonly AppDbContext _context;
    private readonly ILogger<MidnightCleanupJob> _logger;

    public MidnightCleanupJob(
        BackpackCleanupService cleanupService,
        AppDbContext context,
        ILogger<MidnightCleanupJob> logger)
    {
        _cleanupService = cleanupService;
        _context = context;
        _logger = logger;
    }

    public Task ExecuteAsync()
    => ExecuteAsync(CancellationToken.None);

    /// <summary>
    /// Выполняет очистку рюкзаков с учётом часовых поясов
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Запуск задачи проверки очистки рюкзаков");
        
        try
        {
            var utcNow = DateTime.UtcNow;
            
            var childTimezones = await _context.Children
                .AsNoTracking()
                .Select(c => new { c.ChildId, c.TimeZoneId })
                .ToListAsync(cancellationToken);
            
            var cleanedCount = 0;

            foreach (var childInfo in childTimezones)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    TimeZoneInfo timeZone;
                    try
                    {
                        timeZone = TimeZoneInfo.FindSystemTimeZoneById(childInfo.TimeZoneId ?? "UTC");
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        _logger.LogWarning(
                            "Часовой пояс {TimeZoneId} не найден для ребёнка {ChildId}. Используется UTC.",
                            childInfo.TimeZoneId, childInfo.ChildId);
                        timeZone = TimeZoneInfo.Utc;
                    }

                    var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);

                    if (localNow.Hour == 0 && localNow.Minute <= 1)
                    {
                        await _cleanupService.CleanupBackpackForChildAsync(childInfo.ChildId);
                        cleanedCount++;
                        
                        _logger.LogInformation(
                            "Рюкзак очищен для ребёнка {ChildId} в {LocalTime} ({TimeZone})",
                            childInfo.ChildId, localNow, timeZone.DisplayName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Ошибка при очистке рюкзака для ребёнка {ChildId}", 
                        childInfo.ChildId);
                }
            }

            if (cleanedCount > 0)
            {
                _logger.LogInformation(
                    "Задача очистки рюкзаков завершена. Очищено рюкзаков: {CleanedCount}", 
                    cleanedCount);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Задача очистки рюкзаков была отменена");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выполнении задачи очистки рюкзаков");
            throw; 
        }
    }

    /// <summary>
    /// Планирует выполнение задачи каждую минуту
    /// </summary>
    public static void ScheduleRecurringJob()
    {
        // Планируем выполнение каждую минуту для проверки часовых поясов
        RecurringJob.AddOrUpdate<MidnightCleanupJob>(
            "midnight-backpack-cleanup",
            job => job.ExecuteAsync(),
            Cron.Daily(hour: 0, minute: 0),  // "0 0 * * *"
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc 
            }
        );
    }
}
