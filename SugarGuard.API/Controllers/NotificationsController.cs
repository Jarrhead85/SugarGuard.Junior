using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Контроллер для управления уведомлениями родителям
/// </summary>
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly ITelegramNotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;
    private readonly IChildAccessService _childAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDashboardService _dashboardService;

    public NotificationsController(
        ITelegramNotificationService notificationService,
        ILogger<NotificationsController> logger,
        IChildAccessService childAccess,
        ICurrentUserContext currentUser,
        IDashboardService dashboardService)
    {
        _notificationService = notificationService;
        _logger = logger;
        _childAccess = childAccess;
        _currentUser = currentUser;
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Получить список уведомлений для текущего пользователя.
    /// Агрегирует критические события из всех доступных детей за последние 24 часа.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserNotificationDto>>> GetNotifications(CancellationToken cancellationToken)
    {
        try
        {
            var children = await _childAccess.GetAccessibleChildIdsAsync(cancellationToken);
            var result = new List<UserNotificationDto>();
            var now = DateTime.UtcNow;
            var since = now.AddHours(-24);

            // Administrators monitor platform health and access, not children's
            // medical events. Clinical alerts remain visible to parents and doctors.
            var currentRole = _currentUser.GetRole();
            if (currentRole is UserRole.Admin or UserRole.SupportAdmin)
            {
                return Ok(new List<UserNotificationDto>
                {
                    new()
                    {
                        Title = "Система работает",
                        Description = "Новых административных уведомлений нет",
                        Time = "только что",
                        Type = "ok",
                        IsUnread = false
                    }
                });
            }

            foreach (var childId in children)
            {
                try
                {
                    var summary = await _dashboardService.GetSummaryAsync(childId, cancellationToken);

                    if (summary.LatestGlucose is not null && summary.LatestMeasurementTime is not null)
                    {
                        var timeAgo = GetRelativeTime(summary.LatestMeasurementTime.Value, now);
                        var isCritical = summary.LatestGlucoseUiState == "Critical" || summary.LatestGlucoseUiState == "Danger";

                        result.Add(new UserNotificationDto
                        {
                            Title = isCritical ? "Критический уровень глюкозы" : "Уровень глюкозы",
                            Description = $"{summary.LatestGlucose:F1} ммоль/л {(!isCritical ? "— в пределах нормы" : "— требуется внимание!")}",
                            Time = timeAgo,
                            Type = isCritical ? "danger" : "info",
                            IsUnread = isCritical
                        });
                    }

                    if (summary.CriticalEvents > 0)
                    {
                        result.Add(new UserNotificationDto
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
                        result.Add(new UserNotificationDto
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
                        result.Add(new UserNotificationDto
                        {
                            Title = "Экспорт данных",
                            Description = $"{summary.PendingExportJobs} заданий на экспорт в очереди",
                            Time = "в обработке",
                            Type = "info",
                            IsUnread = false
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Ошибка получения сводки для ChildId={ChildId}", childId);
                }
            }

            if (result.Count == 0)
            {
                result.Add(new UserNotificationDto
                {
                    Title = "Всё в порядке",
                    Description = "Критических событий нет",
                    Time = "только что",
                    Type = "ok",
                    IsUnread = false
                });
            }

            return Ok(result.OrderByDescending(n => n.IsUnread).ThenBy(n => n.Type).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения уведомлений");
            return StatusCode(500, new { error = "internal_error", message = "Не удалось получить уведомления." });
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

    /// <summary>
    /// Отправить критическое уведомление с геолокацией
    /// </summary>
    [HttpPost("critical-alert")]
    public async Task<ActionResult<NotificationResponse>> SendCriticalAlert([FromBody] CriticalAlertRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                _logger.LogWarning("Невалидные данные в критическом уведомлении: {Errors}", string.Join(", ", errors));
                return BadRequest(new { error = "validation_error", details = errors });
            }

            _logger.LogWarning("Получен запрос на критическое уведомление для {ChildId}: {CriticalGlucose} ммоль/л", 
                request.ChildId, request.CriticalGlucose);

            if (!Guid.TryParse(request.ChildId, out var criticalChildId) || !await _childAccess.CanAccessChildAsync(criticalChildId))
            {
                return Forbid();
            }

            // Отправляем критическое уведомление
            var result = await _notificationService.SendCriticalAlertAsync(request);

            if (result.Success)
            {
                _logger.LogInformation("Критическое уведомление отправлено {ParentsCount} родителям", 
                    result.ParentsNotified);
                return Ok(result);
            }
            else
            {
                _logger.LogError("Не удалось отправить критическое уведомление: {Error}", 
                    result.ErrorMessage);
                return StatusCode(500, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Исключение при отправке критического уведомления");
            return StatusCode(500, new NotificationResponse
            {
                Success = false,
                ParentsNotified = 0,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Отправить уведомление об измерении глюкозы
    /// </summary>
    [HttpPost("measurement")]
    public async Task<ActionResult<NotificationResponse>> SendMeasurementNotification([FromBody] MeasurementNotificationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { error = "validation_error", details = errors });
            }

            _logger.LogInformation("Получен запрос на уведомление об измерении для {ChildId}: {GlucoseValue} ммоль/л", 
                request.ChildId, request.GlucoseValue);

            if (!Guid.TryParse(request.ChildId, out var measurementChildId) || !await _childAccess.CanAccessChildAsync(measurementChildId))
            {
                return Forbid();
            }

            // Отправляем уведомление об измерении
            var result = await _notificationService.SendMeasurementNotificationAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return StatusCode(500, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Исключение при отправке уведомления об измерении");
            return StatusCode(500, new NotificationResponse
            {
                Success = false,
                ParentsNotified = 0,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Отправить уведомление о съеденном перекусе
    /// </summary>
    [HttpPost("snack-consumed")]
    public async Task<ActionResult<NotificationResponse>> SendSnackConsumedNotification([FromBody] SnackConsumedNotificationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { error = "validation_error", details = errors });
            }

            _logger.LogInformation("Получен запрос на уведомление о перекусе для {ChildId}: {SnackName}", 
                request.ChildId, request.SnackName);

            if (!Guid.TryParse(request.ChildId, out var snackChildId) || !await _childAccess.CanAccessChildAsync(snackChildId))
            {
                return Forbid();
            }

            // Отправляем уведомление о перекусе
            var result = await _notificationService.SendSnackConsumedNotificationAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return StatusCode(500, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Исключение при отправке уведомления о перекусе");
            return StatusCode(500, new NotificationResponse
            {
                Success = false,
                ParentsNotified = 0,
                ErrorMessage = ex.Message
            });
        }
    }
}
