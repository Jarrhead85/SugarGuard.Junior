using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Filters;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Защищённый транспорт очереди уведомлений для Telegram-бота.
/// </summary>
[BotServiceApiKey]
[AllowAnonymous]
[ApiController]
[Route("api/bot-service/telegram-outbox")]
[Produces("application/json")]
public sealed class TelegramOutboxController : ControllerBase
{
    private readonly ITelegramOutboxService _outbox;

    public TelegramOutboxController(ITelegramOutboxService outbox)
    {
        _outbox = outbox;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TelegramOutboxMessageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TelegramOutboxMessageResponse>>> Claim(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _outbox.ClaimPendingAsync(limit, cancellationToken));
    }

    [HttpPost("{messageId:guid}/delivery/{part}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkPartDelivered(
        Guid messageId,
        TelegramOutboxDeliveryPart part,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(part))
        {
            return BadRequest();
        }

        await _outbox.MarkPartDeliveredAsync(messageId, part, cancellationToken);
        return NoContent();
    }

    [HttpPost("{messageId:guid}/acknowledgement")]
    public async Task<IActionResult> Acknowledge(
        Guid messageId,
        [FromBody] TelegramOutboxAcknowledgementRequest request,
        CancellationToken cancellationToken)
    {
        return await _outbox.AcknowledgeAsync(messageId, request.TelegramUserId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{messageId:guid}/delivery")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Complete(
        Guid messageId,
        [FromBody] TelegramOutboxDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        await _outbox.CompleteAsync(messageId, request, cancellationToken);
        return NoContent();
    }
}
