using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Services;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Управление рюкзаком перекусов ребёнка
/// </summary>
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[ApiController]
[Route("api/backpack")]
[Produces("application/json")]
public class BackpackController : ControllerBase
{
    private readonly IBackpackService _backpack;
    private readonly ITelegramNotificationService _notificationService;
    private readonly IChildAccessService _childAccess;
    private readonly ILogger<BackpackController> _logger;

    public BackpackController(
        IBackpackService backpack,
        ITelegramNotificationService notificationService,
        IChildAccessService childAccess,
        ILogger<BackpackController> logger)
    {
        _backpack = backpack;
        _notificationService = notificationService;
        _childAccess = childAccess;
        _logger = logger;
    }

    // GET api/backpack/{childId}
    /// <summary>
    /// Возвращает содержимое рюкзака ребёнка
    /// </summary>
    [HttpGet("{childId:guid}")]
    [ProducesResponseType(typeof(BackpackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BackpackResponse>> GetBackpack(
        Guid childId,
        CancellationToken cancellationToken)
    {
        if (!await _backpack.ChildExistsAsync(childId, cancellationToken))
        {
            _logger.LogWarning("GetBackpack: ребёнок не найден. ChildId={ChildId}.", childId);
            return this.ProblemWithCode(404, "Child Not Found",
                "Ребёнок не найден", "child_not_found");
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var response = await _backpack.GetAsync(childId, cancellationToken);
            _logger.LogInformation(
                "GetBackpack: ChildId={ChildId} Items={ItemCount}.",
                childId, response!.Items.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetBackpack: внутренняя ошибка. ChildId={ChildId}.", childId);
            return this.ProblemWithCode(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "Внутренняя ошибка сервера.",
                "internal_error");
        }
    }

    // POST api/backpack
    /// <summary>
    /// Добавляет новый перекус в рюкзак ребёнка
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BackpackItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BackpackItemResponse>> AddSnack(
        [FromBody] CreateBackpackItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                error = "validation_error",
                details = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
            });
        }

        if (!await _backpack.ChildExistsAsync(request.ChildId, cancellationToken))
        {
            _logger.LogWarning(
                "AddSnack: ребёнок не найден. ChildId={ChildId}.", request.ChildId);
            return this.ProblemWithCode(404, "Child Not Found",
                "Ребёнок не найден", "child_not_found");
        }

        if (!await _childAccess.CanAccessChildAsync(request.ChildId, cancellationToken))
            return Forbid();

        try
        {
            var actorId = _childAccess.GetCurrentUserId() ?? Guid.Empty;
            var response = await _backpack.AddAsync(request, actorId, cancellationToken);
            return CreatedAtAction(nameof(GetBackpack),
                new { childId = request.ChildId }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddSnack: внутренняя ошибка. ChildId={ChildId}.", request.ChildId);
            return this.ProblemWithCode(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "Внутренняя ошибка сервера.",
                "internal_error");
        }
    }

    // PUT api/backpack/{itemId}
    /// <summary>
    /// Обновляет название и ХЕ позиции рюкзака.
    /// </summary>
    [HttpPut("{itemId:guid}")]
    [ProducesResponseType(typeof(BackpackItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BackpackItemResponse>> UpdateSnack(
        Guid itemId,
        [FromBody] UpdateBackpackItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                error = "validation_error",
                details = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
            });
        }

        var currentUserId = _childAccess.GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();

        try
        {
            var outcome = await _backpack.UpdateAsync(
                itemId,
                request,
                currentUserId.Value,
                cancellationToken);

            return outcome.Status switch
            {
                BackpackUpdateResultStatus.Updated => Ok(outcome.Item),
                BackpackUpdateResultStatus.NotFound => this.ProblemWithCode(404, "Item Not Found",
                    "Позиция рюкзака не найдена", "item_not_found"),
                BackpackUpdateResultStatus.Forbidden => Forbid(),
                _ => this.ProblemWithCode(
                    StatusCodes.Status500InternalServerError,
                    "Internal Server Error",
                    "Неизвестный результат обновления позиции.",
                    "internal_error")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateSnack: внутренняя ошибка. ItemId={ItemId}.", itemId);
            return this.ProblemWithCode(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "Внутренняя ошибка сервера.",
                "internal_error");
        }
    }

    // DELETE api/backpack/{itemId}
    /// <summary>
    /// Удаляет перекус из рюкзака
    /// </summary>

    [HttpDelete("{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveSnack(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var currentUserId = _childAccess.GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();

        try
        {
            var result = await _backpack.RemoveAsync(itemId, currentUserId.Value, cancellationToken);

            return result switch
            {
                BackpackRemoveResult.Removed => NoContent(),
                BackpackRemoveResult.NotFound => this.ProblemWithCode(404, "Item Not Found",
                    "Перекус не найден", "item_not_found"),
                BackpackRemoveResult.Forbidden => Forbid(),
                _ => this.ProblemWithCode(
                    StatusCodes.Status500InternalServerError,
                    "Internal Server Error",
                    "Неизвестный результат удаления перекуса.",
                    "internal_error")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveSnack: внутренняя ошибка. ItemId={ItemId}.", itemId);
            return this.ProblemWithCode(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "Внутренняя ошибка сервера.",
                "internal_error");
        }
    }

    // POST api/backpack/{itemId}/consume
    /// <summary>
    /// Отмечает перекус как съеденный
    /// </summary>

    [HttpPost("{itemId:guid}/consume")]
    [ProducesResponseType(typeof(ConsumeSnackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConsumeSnackResponse>> ConsumeSnack(
        Guid itemId,
        [FromQuery] double currentGlucose,
        CancellationToken cancellationToken)
    {
        var actorId = _childAccess.GetCurrentUserId();
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        BackpackConsumeOutcome outcome;
        try
        {
            outcome = await _backpack.ConsumeAsync(itemId, actorId.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConsumeSnack: внутренняя ошибка. ItemId={ItemId}.", itemId);
            return this.ProblemWithCode(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "Внутренняя ошибка сервера.",
                "internal_error");
        }

        switch (outcome.Status)
        {
            case BackpackConsumeResultStatus.NotFound:
                _logger.LogWarning("ConsumeSnack: перекус не найден. ItemId={ItemId}.", itemId);
                return this.ProblemWithCode(404, "Item Not Found",
                    "Перекус не найден", "item_not_found");

            case BackpackConsumeResultStatus.Forbidden:
                _logger.LogWarning(
                    "ConsumeSnack: доступ запрещён. ItemId={ItemId} UserId={UserId}.",
                    itemId, actorId);
                return Forbid();

            case BackpackConsumeResultStatus.Consumed:
                var result = outcome.Result!;

                var (notificationFailed, notificationError) = await SendNotificationAsync(
                    result, currentGlucose, itemId, cancellationToken);

                return Ok(new ConsumeSnackResponse
                {
                    Message = "Перекус успешно съеден.",
                    SnackName = result.SnackName,
                    BreadUnits = result.BreadUnits,
                    ConsumedAt = result.ConsumedAt,
                    NotificationFailed = notificationFailed,
                    NotificationError = notificationError
                });

            default:
                _logger.LogError(
                    "ConsumeSnack: неизвестный outcome {Status} для ItemId={ItemId}.",
                    outcome.Status, itemId);
                return this.ProblemWithCode(
                    StatusCodes.Status500InternalServerError,
                    "Internal Server Error",
                    "Неизвестный результат потребления перекуса.",
                    "internal_error");
        }
    }

    /// <summary>
    /// Отправляет Telegram-уведомление и возвращает его статус
    /// </summary>
    private async Task<(bool Failed, string? Error)> SendNotificationAsync(
        ConsumeSnackResult result,
        double currentGlucose,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        try
        {
            var notificationResult = await _notificationService
                .SendSnackConsumedNotificationAsync(new SnackConsumedNotificationRequest
                {
                    ChildId = result.ChildId.ToString(),
                    SnackName = result.SnackName,
                    BreadUnits = result.BreadUnits,
                    CurrentGlucose = currentGlucose,
                    ConsumedAt = result.ConsumedAt
                });

            if (notificationResult.Success)
            {
                _logger.LogInformation(
                    "ConsumeSnack: уведомление отправлено {Count} родителям.",
                    notificationResult.ParentsNotified);
                return (false, null);
            }

            _logger.LogWarning(
                "ConsumeSnack: уведомление не отправлено. " +
                "ItemId={ItemId} NotificationFailed={NotificationFailed} Error={Error}.",
                itemId, true, notificationResult.ErrorMessage);
            return (true, notificationResult.ErrorMessage);
        }
        catch (Exception notificationEx)
        {
            _logger.LogError(notificationEx,
                "ConsumeSnack: ошибка Telegram-уведомления. " +
                "ItemId={ItemId} NotificationFailed={NotificationFailed}.",
                itemId, true);
            return (true, $"telegram_exception:{notificationEx.GetType().Name}");
        }
    }
}
