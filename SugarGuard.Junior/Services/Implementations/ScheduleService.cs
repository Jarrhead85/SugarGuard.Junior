// Реализация сервиса управления расписанием измерений
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для управления расписанием измерений глюкозы
/// Хранит времена измерений в локальной БД и синхронизирует с сервером
/// </summary>
public class ScheduleService : IScheduleService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<ScheduleService> _logger;
    private readonly ISyncService _syncService;
    private readonly Func<INotificationService> _getNotificationService;

    public ScheduleService(
        IDbContextFactory<AppDbContext> factory,
        ILogger<ScheduleService> logger,
        ISyncService syncService,
        Func<INotificationService> getNotificationService)
    {
        _factory = factory;
        _logger = logger;
        _syncService = syncService;
        _getNotificationService = getNotificationService;
    }

    /// <summary>
    /// Получает все активные времена измерений для ребёнка
    /// </summary>
    public async Task<List<MeasurementSchedule>> GetScheduleAsync(string childId)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var schedules = await ctx.Set<MeasurementSchedule>()
                .AsNoTracking()
                .Where(s => s.ChildId == childId && s.IsActive)
                .OrderBy(s => s.ScheduledTime)
                .ToListAsync();

            _logger.LogInformation("Получено {Count} времён измерений для ребёнка {ChildId}", 
                schedules.Count, childId);

            return schedules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения расписания для ребёнка {ChildId}", childId);
            return new List<MeasurementSchedule>();
        }
    }

    /// <summary>
    /// Добавляет новое время измерения в расписание
    /// </summary>
    public async Task<bool> AddScheduleItemAsync(string childId, TimeOnly time)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();

            // Проверяем, нет ли уже такого времени
            var exists = await ctx.Set<MeasurementSchedule>()
                .AnyAsync(s => s.ChildId == childId && s.ScheduledTime == time);

            if (exists)
            {
                _logger.LogWarning("Время {Time} уже существует в расписании ребёнка {ChildId}", 
                    time.ToString("HH:mm"), childId);
                return false;
            }

            var schedule = new MeasurementSchedule
            {
                ChildId = childId,
                ScheduledTime = time,
                IsActive = true,
                IsSynced = false
            };

            ctx.Set<MeasurementSchedule>().Add(schedule);
            await ctx.SaveChangesAsync();

            // Добавляем в очередь синхронизации
            await _syncService.QueueItemAsync(schedule.ScheduleId, "MeasurementSchedule", 
                "Insert", System.Text.Json.JsonSerializer.Serialize(schedule));

            _logger.LogInformation("Добавлено время измерения {Time} для ребёнка {ChildId}", 
                time.ToString("HH:mm"), childId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка добавления времени {Time} для ребёнка {ChildId}", 
                time.ToString("HH:mm"), childId);
            return false;
        }
    }

    /// <summary>
    /// Удаляет время измерения из расписания
    /// </summary>
    public async Task<bool> RemoveScheduleItemAsync(string childId, TimeOnly time)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var schedule = await ctx.Set<MeasurementSchedule>()
                .FirstOrDefaultAsync(s => s.ChildId == childId && s.ScheduledTime == time);

            if (schedule == null)
            {
                _logger.LogWarning("Время {Time} не найдено в расписании ребёнка {ChildId}", 
                    time.ToString("HH:mm"), childId);
                return false;
            }

            ctx.Set<MeasurementSchedule>().Remove(schedule);
            await ctx.SaveChangesAsync();

            // Добавляем в очередь синхронизации
            await _syncService.QueueItemAsync(schedule.ScheduleId, "MeasurementSchedule", 
                "Delete", System.Text.Json.JsonSerializer.Serialize(schedule));

            _logger.LogInformation("Удалено время измерения {Time} для ребёнка {ChildId}", 
                time.ToString("HH:mm"), childId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления времени {Time} для ребёнка {ChildId}", 
                time.ToString("HH:mm"), childId);
            return false;
        }
    }

    /// <summary>
    /// Получает следующее запланированное измерение
    /// </summary>
    public async Task<MeasurementSchedule?> GetNextScheduledMeasurementAsync(string childId)
    {
        try
        {
            var schedules = await GetScheduleAsync(childId);
            
            if (!schedules.Any())
            {
                return null;
            }

            var currentTime = TimeOnly.FromDateTime(DateTime.Now);
            
            // Ищем ближайшее время сегодня
            var todaySchedule = schedules
                .Where(s => s.ScheduledTime > currentTime)
                .OrderBy(s => s.ScheduledTime)
                .FirstOrDefault();

            // Если нет времени сегодня, берём первое время завтра
            if (todaySchedule == null)
            {
                todaySchedule = schedules.OrderBy(s => s.ScheduledTime).First();
            }

            _logger.LogInformation("Следующее измерение для ребёнка {ChildId}: {Time}", 
                childId, todaySchedule.FormattedTime);

            return todaySchedule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения следующего измерения для ребёнка {ChildId}", childId);
            return null;
        }
    }

    /// <summary>
    /// Активирует или деактивирует время измерения
    /// </summary>
    public async Task<bool> SetScheduleItemActiveAsync(string scheduleId, bool isActive)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var schedule = await ctx.Set<MeasurementSchedule>()
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);

            if (schedule == null)
            {
                _logger.LogWarning("Элемент расписания {ScheduleId} не найден", scheduleId);
                return false;
            }

            schedule.IsActive = isActive;
            schedule.IsSynced = false;

            await ctx.SaveChangesAsync();

            // Добавляем в очередь синхронизации
            await _syncService.QueueItemAsync(schedule.ScheduleId, "MeasurementSchedule", 
                "Update", System.Text.Json.JsonSerializer.Serialize(schedule));

            _logger.LogInformation("Изменён статус расписания {ScheduleId}: {Status}", 
                scheduleId, isActive ? "активно" : "неактивно");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка изменения статуса расписания {ScheduleId}", scheduleId);
            return false;
        }
    }

    /// <summary>
    /// Проверяет валидность времени (формат HH:MM, нет дубликатов)
    /// </summary>
    public async Task<bool> IsValidScheduleTimeAsync(string childId, TimeOnly time)
    {
        try
        {
            // Проверяем, нет ли уже такого времени
            await using var ctx = await _factory.CreateDbContextAsync();
            var exists = await ctx.Set<MeasurementSchedule>()
                .AnyAsync(s => s.ChildId == childId && s.ScheduledTime == time);

            return !exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка проверки валидности времени {Time} для ребёнка {ChildId}", 
                time.ToString("HH:mm"), childId);
            return false;
        }
    }

    /// <summary>
    /// Инициализирует напоминания для всех детей при запуске приложения
    /// </summary>
    public async Task<bool> InitializeAllRemindersAsync()
    {
        try
        {
            var notificationService = _getNotificationService();

            // Получаем всех детей (read-only, без отслеживания)
            await using var ctx = await _factory.CreateDbContextAsync();
            var children = await ctx.Set<Child>()
                .AsNoTracking()
                .ToListAsync();

            var tasks = children.Select(async child =>
            {
                _logger.LogInformation("Инициализация напоминаний для ребёнка {ChildId}", child.ChildId);
                var scheduled = await notificationService.ScheduleAllRemindersAsync(child.ChildId);
                if (!scheduled)
                {
                    _logger.LogWarning("Не удалось запланировать напоминания для ребёнка {ChildId}", child.ChildId);
                }
            });
            await Task.WhenAll(tasks);

            _logger.LogInformation("Инициализация напоминаний завершена для {Count} детей", children.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации напоминаний");
            return false;
        }
    }
}