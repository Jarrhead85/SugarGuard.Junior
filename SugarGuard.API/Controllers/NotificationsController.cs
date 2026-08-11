using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly IUserNotificationService _userNotificationService;
    private readonly IWebPushService _webPushService;

    public NotificationsController(
        ITelegramNotificationService notificationService,
        ILogger<NotificationsController> logger,
        IChildAccessService childAccess,
        IUserNotificationService userNotificationService,
        IWebPushService webPushService)
    {
        _notificationService = notificationService;
        _logger = logger;
        _childAccess = childAccess;
        _userNotificationService = userNotificationService;
        _webPushService = webPushService;
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
            var result = await _userNotificationService.GetForCurrentUserAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения уведомлений");
            return StatusCode(500, new { error = "internal_error", message = "Не удалось получить уведомления." });
        }
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var updated = await _userNotificationService.MarkAllAsReadAsync(cancellationToken);
        return Ok(new { updated });
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var updated = await _userNotificationService.MarkAsReadAsync(notificationId, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>
    /// Отправить критическое уведомление с геолокацией
    /// </summary>
    [HttpPost("critical-alert")]
    [Authorize(Policy = "ChildSafetyEvent")]
    [EnableRateLimiting("safety-events")]
    public async Task<ActionResult<NotificationResponse>> SendCriticalAlert(
        [FromBody] CriticalAlertRequest request,
        CancellationToken cancellationToken)
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

            _logger.LogWarning("Critical safety event received.");

            if (!Guid.TryParse(request.ChildId, out var criticalChildId) || !await _childAccess.CanAccessChildAsync(criticalChildId))
            {
                return Forbid();
            }

            await _userNotificationService.PersistCriticalLocationAsync(request, cancellationToken);

            var notificationTitle = request.IsEmergencyHelp
                ? "SOS от ребёнка"
                : "Критический уровень глюкозы";
            var notificationBody = request.IsEmergencyHelp
                ? $"Ребёнок просит помощи. Глюкоза: {request.CriticalGlucose:F1} ммоль/л."
                : $"{request.CriticalGlucose:F1} ммоль/л. Требуется внимание.";

            await SendWebPushAsync(
                criticalChildId,
                notificationTitle,
                notificationBody,
                $"/parent/dashboard?childId={criticalChildId}&tab=alerts",
                requireInteraction: true,
                cancellationToken: cancellationToken);

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
                _logger.LogWarning("Критическое уведомление сохранено в кабинете, но Telegram недоступен: {Error}", 
                    result.ErrorMessage);
                return Ok(new NotificationResponse
                {
                    Success = true,
                    ParentsNotified = result.ParentsNotified,
                    ErrorMessage = "Уведомление сохранено в кабинете родителя; Telegram пока недоступен."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Исключение при отправке критического уведомления");
            return StatusCode(500, new NotificationResponse
            {
                Success = false,
                ParentsNotified = 0,
                ErrorMessage = "Не удалось отправить критическое уведомление."
            });
        }
    }

    /// <summary>
    /// Отправить уведомление об измерении глюкозы
    /// </summary>
    [HttpPost("measurement")]
    [Authorize(Policy = "ChildSafetyEvent")]
    [EnableRateLimiting("safety-events")]
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

            _logger.LogInformation("Measurement notification request received.");

            if (!Guid.TryParse(request.ChildId, out var measurementChildId) || !await _childAccess.CanAccessChildAsync(measurementChildId))
            {
                return Forbid();
            }

            // Отправляем уведомление об измерении
            await SendWebPushAsync(
                measurementChildId,
                "Новое измерение глюкозы",
                $"{request.GlucoseValue:F1} ммоль/л · {request.Status}",
                $"/parent/dashboard?childId={measurementChildId}&tab=measurements");

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
                ErrorMessage = "Не удалось отправить уведомление об измерении."
            });
        }
    }

    private async Task SendWebPushAsync(
        Guid childId,
        string title,
        string body,
        string? url = null,
        bool requireInteraction = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _webPushService.SendForChildAsync(
                childId,
                title,
                body,
                url,
                requireInteraction,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Не удалось отправить Web Push. ChildId={ChildId}", childId);
        }
    }

    /// <summary>
    /// Отправить уведомление о съеденном перекусе
    /// </summary>
    [HttpPost("snack-consumed")]
    [Authorize(Policy = "ChildSafetyEvent")]
    [EnableRateLimiting("safety-events")]
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

            _logger.LogInformation("Snack notification request received.");

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
                ErrorMessage = "Не удалось отправить уведомление о перекусе."
            });
        }
    }
}
