using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Services;

public sealed class UserNotificationService : IUserNotificationService
{
    private readonly AppDbContext _db;
    private readonly IChildAccessService _childAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<UserNotificationService> _logger;

    public UserNotificationService(
        AppDbContext db,
        IChildAccessService childAccess,
        ICurrentUserContext currentUser,
        IDashboardService dashboardService,
        ILogger<UserNotificationService> logger)
    {
        _db = db;
        _childAccess = childAccess;
        _currentUser = currentUser;
        _dashboardService = dashboardService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserNotificationDto>> GetForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var role = _currentUser.GetRole();
        if (role is UserRole.Admin or UserRole.SupportAdmin)
        {
            return
            [
                new UserNotificationDto
                {
                    Title = "Система работает",
                    Description = "Новых административных уведомлений нет",
                    Time = "только что",
                    Type = "ok",
                    IsUnread = false
                }
            ];
        }

        var userId = _currentUser.GetUserId()
            ?? throw new UnauthorizedAccessException("Текущий пользователь не определён.");
        var childIds = await _childAccess.GetAccessibleChildIdsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var result = await _db.UserNotifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(100)
            .Select(notification => new
            {
                notification.NotificationId,
                notification.Title,
                notification.Description,
                notification.Type,
                notification.IsRead,
                notification.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var notifications = result.Select(notification => new UserNotificationDto
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            Description = notification.Description,
            Time = GetRelativeTime(notification.CreatedAt, now),
            Type = notification.Type,
            IsUnread = !notification.IsRead
        }).ToList();

        foreach (var childId in childIds)
        {
            try
            {
                var summary = await _dashboardService.GetSummaryAsync(childId, cancellationToken);
                AddClinicalNotifications(notifications, summary, now);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogDebug(
                    exception,
                    "Не удалось получить сводку уведомлений для ChildId={ChildId}",
                    childId);
            }
        }

        if (notifications.Count == 0)
        {
            notifications.Add(new UserNotificationDto
            {
                Title = "Всё в порядке",
                Description = "Новых событий нет",
                Time = "только что",
                Type = "ok",
                IsUnread = false
            });
        }

        return notifications
            .OrderByDescending(notification => notification.IsUnread)
            .ThenBy(notification => notification.Type)
            .ToList();
    }

    public async Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetUserId()
            ?? throw new UnauthorizedAccessException("Текущий пользователь не определён.");

        return await _db.UserNotifications
            .Where(notification => notification.RecipientUserId == userId && !notification.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(notification => notification.IsRead, true),
                cancellationToken);
    }

    private static void AddClinicalNotifications(
        ICollection<UserNotificationDto> notifications,
        DashboardSummaryResponse summary,
        DateTime now)
    {
        if (summary.LatestGlucose is not null && summary.LatestMeasurementTime is not null)
        {
            var isCritical = summary.LatestGlucoseUiState is "Critical" or "Danger";
            notifications.Add(new UserNotificationDto
            {
                Title = isCritical ? "Критический уровень глюкозы" : "Уровень глюкозы",
                Description = $"{summary.LatestGlucose:F1} ммоль/л {(isCritical ? "— требуется внимание!" : "— в пределах нормы")}",
                Time = GetRelativeTime(summary.LatestMeasurementTime.Value, now),
                Type = isCritical ? "danger" : "info",
                IsUnread = isCritical
            });
        }

        if (summary.CriticalEvents > 0)
        {
            notifications.Add(new UserNotificationDto
            {
                Title = "Критические эпизоды",
                Description = $"Зафиксировано {summary.CriticalEvents} критических эпизодов за последние 24 часа",
                Time = "за сутки",
                Type = "danger",
                IsUnread = true
            });
        }

        if (summary.PendingSyncConflicts > 0)
        {
            notifications.Add(new UserNotificationDto
            {
                Title = "Конфликты синхронизации",
                Description = $"{summary.PendingSyncConflicts} неразрешённых конфликтов",
                Time = "требуют внимания",
                Type = "warn",
                IsUnread = true
            });
        }

        if (summary.PendingExportJobs > 0)
        {
            notifications.Add(new UserNotificationDto
            {
                Title = "Экспорт данных",
                Description = $"{summary.PendingExportJobs} заданий на экспорт в очереди",
                Time = "в обработке",
                Type = "info",
                IsUnread = false
            });
        }
    }

    private static string GetRelativeTime(DateTime utcTime, DateTime now)
    {
        var diff = now - utcTime;
        if (diff.TotalMinutes < 1) return "только что";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} мин назад";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} ч назад";
        return $"{(int)diff.TotalDays} дн назад";
    }
}
