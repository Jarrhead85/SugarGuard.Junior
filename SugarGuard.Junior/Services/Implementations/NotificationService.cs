// Реализация сервиса уведомлений
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для отправки уведомлений и управления напоминаниями
/// Поддерживает локальные push-уведомления и планирование напоминаний
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IScheduleService _scheduleService;
    private readonly IApiClient _apiClient;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    
    // Словарь пропущенных измерений для повторных напоминаний
    private readonly ConcurrentDictionary<string, DateTime> _missedMeasurements = new();

    // Множество запланированных ID уведомлений (для пакетной отмены)
    private readonly ConcurrentDictionary<string, byte> _scheduledNotificationIds = new();

    public NotificationService(
        ILogger<NotificationService> logger,
        IScheduleService scheduleService,
        IApiClient apiClient,
        IDbContextFactory<AppDbContext> dbFactory)
    {
        _logger = logger;
        _scheduleService = scheduleService;
        _apiClient = apiClient;
        _dbFactory = dbFactory;
    }

    /// <summary>
    /// Отправляет локальное уведомление на устройство через Plugin.LocalNotification
    /// </summary>
    public async Task<bool> SendLocalNotificationAsync(string title, string message, string notificationId)
    {
        try
        {
            _logger.LogInformation("Локальное уведомление [{NotificationId}]: {Title} - {Message}", 
                notificationId, title, message);

            // Запрос разрешения (Android 13+)
            var permissionResult = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (permissionResult != PermissionStatus.Granted)
            {
                _logger.LogWarning("Разрешение на уведомления не получено: {Status}", permissionResult);
                return false;
            }

            await Plugin.LocalNotification.LocalNotificationCenter.Current.Show(new Plugin.LocalNotification.NotificationRequest
            {
                NotificationId = CreateStableNotificationId(notificationId),
                Title = title,
                Description = message,
                CategoryType = Plugin.LocalNotification.NotificationCategoryType.Alarm,
                Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                {
                    Priority = Plugin.LocalNotification.AndroidOption.AndroidPriority.High,
                    ChannelId = "glucose_alerts"
                }
            });

            _logger.LogInformation("Локальное уведомление отправлено: {NotificationId}", notificationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки локального уведомления {NotificationId}", notificationId);
            return false;
        }
    }

    /// <summary>
    /// Отправляет SMS родителю в критической ситуации
    /// </summary>
    public async Task<bool> SendSMSAsync(string phoneNumber, string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("SMS не отправлено: номер или текст пустой");
                return false;
            }

            if (!Microsoft.Maui.ApplicationModel.Communication.Sms.Default.IsComposeSupported)
            {
                _logger.LogWarning("Создание SMS не поддерживается на этом устройстве");
                return false;
            }

            var sms = new Microsoft.Maui.ApplicationModel.Communication.SmsMessage(message, [phoneNumber]);
            await Microsoft.Maui.ApplicationModel.Communication.Sms.Default.ComposeAsync(sms);
            _logger.LogInformation("Открыт системный редактор SMS для {PhoneNumber}", phoneNumber);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки SMS на {PhoneNumber}", phoneNumber);
            return false;
        }
    }

    /// <summary>
    /// Планирует уведомление на определённое время
    /// </summary>
    public async Task<bool> ScheduleNotificationAsync(string title, string message, string notificationId, DateTime scheduledTime)
    {
        try
        {
            // Отменяем существующий таймер если есть
            await CancelNotificationAsync(notificationId);

            var delay = scheduledTime - DateTime.Now;
            if (delay.TotalMilliseconds <= 0)
            {
                // Время уже прошло, отправляем сразу
                return await SendLocalNotificationAsync(title, message, notificationId);
            }

            var scheduledNotification = new Plugin.LocalNotification.NotificationRequest
            {
                NotificationId = CreateStableNotificationId(notificationId),
                Title = title,
                Description = message,
                Schedule = new Plugin.LocalNotification.NotificationRequestSchedule
                {
                    NotifyTime = scheduledTime
                },
                CategoryType = Plugin.LocalNotification.NotificationCategoryType.Reminder,
                Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                {
                    ChannelId = notificationId.StartsWith("measurement_reminder_")
                        ? "measurement_reminders"
                        : "glucose_alerts",
                    Priority = Plugin.LocalNotification.AndroidOption.AndroidPriority.High
                }
            };

            await Plugin.LocalNotification.LocalNotificationCenter.Current.Show(scheduledNotification);
            _scheduledNotificationIds.TryAdd(notificationId, 0);

            _logger.LogInformation("Запланировано уведомление {NotificationId} на {ScheduledTime}", 
                notificationId, scheduledTime.ToString("HH:mm"));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка планирования уведомления {NotificationId}", notificationId);
            return false;
        }
    }

    private static int CreateStableNotificationId(string key)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt32(hashBytes, 0) & int.MaxValue;
    }

    /// <summary>
    /// Отменяет запланированное уведомление.
    /// </summary>
    public async Task<bool> CancelNotificationAsync(string notificationId)
    {
        try
        {
            Plugin.LocalNotification.LocalNotificationCenter.Current.Cancel(CreateStableNotificationId(notificationId));
            _scheduledNotificationIds.TryRemove(notificationId, out _);
            _logger.LogInformation("Отменено запланированное уведомление {NotificationId}", notificationId);

            // Также отменяем повторные напоминания если это измерение
            if (notificationId.StartsWith("measurement_reminder_"))
            {
                var childId = notificationId.Replace("measurement_reminder_", "");
                _missedMeasurements.TryRemove(childId, out _);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отмены уведомления {NotificationId}", notificationId);
            return false;
        }
    }

    /// <summary>
    /// Отправляет полноэкранное уведомление при критическом уровне
    /// с вибрацией и звуковым сигналом
    /// </summary>
    public async Task<bool> SendCriticalAlertAsync(string title, string message, double glucoseValue)
    {
        try
        {
            var criticalId = $"critical_alert_{Guid.NewGuid()}";
            
            _logger.LogError("КРИТИЧЕСКОЕ УВЕДОМЛЕНИЕ: {Title} - Глюкоза: {GlucoseValue} ммоль/л", 
                title, glucoseValue);

            // Вибрация
            try
            {
                var vibrationDuration = TimeSpan.FromMilliseconds(1500);
                Vibration.Default.Vibrate(vibrationDuration);
            }
            catch (Exception vibrEx)
            {
                _logger.LogWarning(vibrEx, "Не удалось запустить вибрацию");
            }

            // Отправляем через Plugin.LocalNotification с высоким приоритетом
            var permissionResult = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (permissionResult == PermissionStatus.Granted)
            {
                await Plugin.LocalNotification.LocalNotificationCenter.Current.Show(new Plugin.LocalNotification.NotificationRequest
                {
                    NotificationId = criticalId.GetHashCode(),
                    Title = $" {title}",
                    Description = $"{message} (глюкоза: {glucoseValue:F1} ммоль/л)",
                    CategoryType = Plugin.LocalNotification.NotificationCategoryType.Alarm,
                    Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                    {
                        Priority = Plugin.LocalNotification.AndroidOption.AndroidPriority.Max,
                        ChannelId = "glucose_critical",
                        Ongoing = true
                    }
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки критического уведомления");
            return false;
        }
    }

    /// <summary>
    /// Планирует все напоминания для ребёнка на основе расписания
    /// </summary>
    public async Task<bool> ScheduleAllRemindersAsync(string childId)
    {
        try
        {
            // Отменяем все существующие напоминания для этого ребёнка
            await CancelAllRemindersAsync(childId);

            // Получаем расписание
            var schedules = await _scheduleService.GetScheduleAsync(childId);
            
            var scheduledCount = 0;
            var today = DateTime.Today;

            foreach (var schedule in schedules.Where(s => s.IsActive))
            {
                // Планируем на сегодня (если время ещё не прошло) и на завтра
                for (int dayOffset = 0; dayOffset <= 1; dayOffset++)
                {
                    var scheduledDateTime = today.AddDays(dayOffset).Add(schedule.ScheduledTime.ToTimeSpan());
                    
                    // Пропускаем прошедшие времена сегодня
                    if (dayOffset == 0 && scheduledDateTime <= DateTime.Now)
                        continue;

                    var notificationId = $"measurement_reminder_{childId}_{schedule.ScheduleId}_{dayOffset}";
                    var title = "⏰ Время измерения глюкозы";
                    var message = $"Пора измерить уровень глюкозы ({schedule.FormattedTime})";

                    await ScheduleNotificationAsync(title, message, notificationId, scheduledDateTime);
                    scheduledCount++;
                }
            }

            _logger.LogInformation("Запланировано {Count} напоминаний для ребёнка {ChildId}", 
                scheduledCount, childId);

            // Проверяем, не пропущено ли измерение по уже прошедшему времени расписания
            await CheckForMissedMeasurementsAsync(childId, schedules, today);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка планирования напоминаний для ребёнка {ChildId}", childId);
            return false;
        }
    }

    private async Task CheckForMissedMeasurementsAsync(string childId, List<MeasurementSchedule> schedules, DateTime today)
    {
        try
        {
            // Если последнее измерение было сделано в течение последних 2 часов - не считаем пропущенным
            await using var ctx = await _dbFactory.CreateDbContextAsync();
            var latestMeasurementTime = await ctx.Set<MeasurementEntity>()
                .Where(m => m.ChildId == childId)
                .OrderByDescending(m => m.MeasurementTime)
                .Select(m => (DateTime?)m.MeasurementTime)
                .FirstOrDefaultAsync();

            if (latestMeasurementTime.HasValue && latestMeasurementTime.Value >= DateTime.Now.AddHours(-2))
                return;

            // Проверяем, есть ли запланированное время, которое уже прошло (но не более 2 часов назад)
            var missedSchedule = schedules
                .Where(s => s.IsActive)
                .Select(s => today.Add(s.ScheduledTime.ToTimeSpan()))
                .FirstOrDefault(scheduledTime =>
                {
                    var diff = DateTime.Now - scheduledTime;
                    return diff.TotalMinutes > 0 && diff.TotalHours <= 2;
                });

            if (missedSchedule != default)
            {
                _logger.LogWarning("Обнаружено пропущенное измерение для ребёнка {ChildId} (время {ScheduledTime:HH:mm})",
                    childId, missedSchedule);
                await StartMissedMeasurementRemindersAsync(childId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке пропущенных измерений для {ChildId}", childId);
        }
    }

    /// <summary>
    /// Отменяет все напоминания для ребёнка
    /// </summary>
    public async Task<bool> CancelAllRemindersAsync(string childId)
    {
        try
        {
            var remindersToCancel = _scheduledNotificationIds.Keys
                .Where(id => id.Contains($"measurement_reminder_{childId}"))
                .ToList();

            foreach (var notificationId in remindersToCancel)
            {
                await CancelNotificationAsync(notificationId);
            }

            _logger.LogInformation("Отменено {Count} напоминаний для ребёнка {ChildId}", 
                remindersToCancel.Count, childId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отмены напоминаний для ребёнка {ChildId}", childId);
            return false;
        }
    }

    /// <summary>
    /// Отмечает измерение как выполненное (останавливает повторные напоминания)
    /// </summary>
    public async Task<bool> MarkMeasurementCompletedAsync(string childId)
    {
        try
        {
            _missedMeasurements.TryRemove(childId, out _);
            
            // Отменяем все активные напоминания о пропущенном измерении
            var missedReminders = _scheduledNotificationIds.Keys
                .Where(id => id.StartsWith($"missed_measurement_{childId}"))
                .ToList();

            foreach (var notificationId in missedReminders)
            {
                await CancelNotificationAsync(notificationId);
            }

            _logger.LogInformation("Измерение отмечено как выполненное для ребёнка {ChildId}", childId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отметки измерения для ребёнка {ChildId}", childId);
            return false;
        }
    }

    /// <summary>
    /// Запускает повторные напоминания о пропущенном измерении (каждые 5 минут)
    /// </summary>
    private async Task StartMissedMeasurementRemindersAsync(string childId)
    {
        try
        {
            // Отмечаем время пропущенного измерения
            var missedTime = DateTime.Now;
            _missedMeasurements[childId] = missedTime;

            // Планируем повторные напоминания каждые 5 минут
            for (int i = 1; i <= 12; i++) // Максимум 12 напоминаний (1 час)
            {
                var delay = i; // захват по значению, не по ссылке (избегаем closure bug)
                var reminderTime = DateTime.Now.AddMinutes(delay * 5);
                var notificationId = $"missed_measurement_{childId}_{delay}";
                var title = " Пропущенное измерение";
                var message = $"Вы пропустили измерение глюкозы. Пожалуйста, измерьте уровень сахара.";

                // Планируем локальное напоминание
                await ScheduleNotificationAsync(title, message, notificationId, reminderTime);

                // Планируем уведомление родителям через API (в фоне)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(delay * 5));
                        await SendMissedMeasurementNotificationToParentsAsync(childId, missedTime, delay);
                    }
                    catch (OperationCanceledException)
                    {
                        // ожидаемо при отмене
                    }
                    catch (Exception apiEx)
                    {
                        _logger.LogError(apiEx, "Ошибка отправки уведомления родителям о пропущенном измерении (напоминание {Delay})", delay);
                    }
                });
            }

            _logger.LogWarning("Запущены повторные напоминания о пропущенном измерении для ребёнка {ChildId}", childId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка запуска повторных напоминаний для ребёнка {ChildId}", childId);
        }
    }

    /// <summary>
    /// Отправляет уведомление родителям о пропущенном измерении через API
    /// </summary>
    private async Task SendMissedMeasurementNotificationToParentsAsync(string childId, DateTime missedTime, int reminderNumber)
    {
        try
        {
            // Получаем следующее запланированное измерение для определения времени
            var nextSchedule = await _scheduleService.GetNextScheduledMeasurementAsync(childId);
            if (nextSchedule == null)
            {
                _logger.LogWarning("Не найдено расписание для ребёнка {ChildId}", childId);
                return;
            }

            // Вычисляем запланированное время (ближайшее к времени пропуска)
            var scheduledTime = DateTime.Today.Add(nextSchedule.ScheduledTime.ToTimeSpan());
            if (scheduledTime > missedTime)
            {
                scheduledTime = scheduledTime.AddDays(-1); // Было вчера
            }

            var minutesLate = (int)(missedTime - scheduledTime).TotalMinutes;

            // Создаём запрос для API
            var request = new Models.Api.MissedMeasurementNotificationRequest
            {
                ChildId = childId,
                ScheduledTime = scheduledTime,
                MissedAt = missedTime,
                MinutesLate = minutesLate,
                ReminderNumber = reminderNumber
            };

            // Отправляем через API
            _logger.LogWarning("Отправка уведомления родителям о пропущенном измерении #{ReminderNumber} для ребёнка {ChildId}", 
                reminderNumber, childId);

            var success = await _apiClient.SendMissedMeasurementNotificationAsync(request);
            if (success)
            {
                _logger.LogInformation("Уведомление родителям о пропущенном измерении отправлено успешно");
            }
            else
            {
                _logger.LogError("Не удалось отправить уведомление родителям о пропущенном измерении");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки уведомления родителям о пропущенном измерении");
        }
    }

    /// <summary>
    /// Освобождает ресурсы при завершении работы сервиса
    /// </summary>
    public void Dispose()
    {
        _scheduledNotificationIds.Clear();
        _missedMeasurements.Clear();
    }
}
