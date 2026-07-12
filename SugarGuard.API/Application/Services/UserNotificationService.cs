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
        var userId = _currentUser.GetUserId()
            ?? throw new UnauthorizedAccessException("Текущий пользователь не определён.");

        if (role is UserRole.Admin or UserRole.SupportAdmin)
        {
            var supportNotifications = await _db.UserNotifications
                .AsNoTracking()
                .Where(notification =>
                    notification.RecipientUserId == userId &&
                    (notification.SourceType == "support_message" ||
                     notification.SourceType == "support_conversation"))
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

            return supportNotifications.Select(notification => new UserNotificationDto
            {
                NotificationId = notification.NotificationId,
                Title = notification.Title,
                Description = notification.Description,
                Time = GetRelativeTime(notification.CreatedAt, DateTime.UtcNow),
                Type = notification.Type,
                IsUnread = !notification.IsRead
            }).ToList();
        }

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

        return notifications;
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

    public async Task PersistCriticalLocationAsync(
        CriticalAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.ChildId, out var childId))
        {
            throw new ArgumentException("Некорректный идентификатор ребёнка.", nameof(request));
        }

        var parentIds = await _db.ParentChildLinks
            .AsNoTracking()
            .Where(link => link.ChildId == childId)
            .Select(link => link.ParentUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (parentIds.Count == 0)
        {
            return;
        }

        var duplicateSince = DateTime.UtcNow.AddMinutes(-2);
        var recipientsWithDuplicate = await _db.UserNotifications
            .AsNoTracking()
            .Where(notification =>
                parentIds.Contains(notification.RecipientUserId) &&
                notification.ChildId == childId &&
                notification.SourceType == "critical_location" &&
                notification.CreatedAt >= duplicateSince)
            .Select(notification => notification.RecipientUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var locationText = request.Latitude.HasValue && request.Longitude.HasValue
            ? $"Координаты: {request.Latitude.Value:F6}, {request.Longitude.Value:F6}. " +
              $"Карта: https://www.openstreetmap.org/?mlat={request.Latitude.Value:F6}&mlon={request.Longitude.Value:F6}#map=17/{request.Latitude.Value:F6}/{request.Longitude.Value:F6}"
            : "Координаты получить не удалось.";

        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            locationText = $"Адрес: {request.Address}. {locationText}";
        }

        foreach (var parentId in parentIds.Except(recipientsWithDuplicate))
        {
            _db.UserNotifications.Add(new SugarGuard.Domain.Entities.UserNotification
            {
                RecipientUserId = parentId,
                ChildId = childId,
                Type = "danger",
                Title = "Критический уровень глюкозы",
                Description = $"{request.CriticalGlucose:F1} ммоль/л. {locationText}",
                SourceType = "critical_location",
                SourceId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task PersistMeasurementAsync(
        Guid childId,
        Guid measurementId,
        decimal glucoseValue,
        string status,
        DateTime measuredAt,
        bool isCritical,
        CancellationToken cancellationToken = default)
    {
        var title = isCritical
            ? "Критический уровень глюкозы"
            : "Новое измерение глюкозы";
        var description = $"{glucoseValue:F1} ммоль/л · {status}";
        var type = isCritical ? "danger" : "info";

        await PersistForParentsAsync(
            childId,
            "measurement",
            measurementId,
            type,
            title,
            description,
            measuredAt,
            cancellationToken);
    }

    public async Task PersistSnackConsumedAsync(
        Guid childId,
        Guid backpackItemId,
        string snackName,
        decimal breadUnits,
        double currentGlucose,
        DateTime consumedAt,
        CancellationToken cancellationToken = default)
    {
        var glucoseText = currentGlucose > 0
            ? $" · сахар {currentGlucose:F1} ммоль/л"
            : string.Empty;

        await PersistForParentsAsync(
            childId,
            "snack_consumed",
            backpackItemId,
            "ok",
            "Съеден перекус",
            $"{snackName}, {breadUnits:0.#} ХЕ{glucoseText}",
            consumedAt,
            cancellationToken);
    }

    private async Task PersistForParentsAsync(
        Guid childId,
        string sourceType,
        Guid sourceId,
        string type,
        string title,
        string description,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        var parentIds = await _db.ParentChildLinks
            .AsNoTracking()
            .Where(link => link.ChildId == childId)
            .Select(link => link.ParentUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (parentIds.Count == 0)
        {
            return;
        }

        var existingRecipients = await _db.UserNotifications
            .AsNoTracking()
            .Where(notification =>
                parentIds.Contains(notification.RecipientUserId) &&
                notification.SourceType == sourceType &&
                notification.SourceId == sourceId)
            .Select(notification => notification.RecipientUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var parentId in parentIds.Except(existingRecipients))
        {
            _db.UserNotifications.Add(new SugarGuard.Domain.Entities.UserNotification
            {
                RecipientUserId = parentId,
                ChildId = childId,
                Type = type,
                Title = title,
                Description = description,
                SourceType = sourceType,
                SourceId = sourceId,
                CreatedAt = createdAt.Kind == DateTimeKind.Utc ? createdAt : createdAt.ToUniversalTime(),
                IsRead = false
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
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
                // Сводка выводится как справочная карточка. Непрочитанными считаются
                // только сохранённые уведомления, которые можно отметить прочитанными.
                IsUnread = false
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
                IsUnread = false
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
                IsUnread = false
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
