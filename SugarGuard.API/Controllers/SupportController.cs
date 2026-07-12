using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Controllers;

[Authorize]
[ApiController]
[Route("api/support")]
public sealed class SupportController : ControllerBase
{
    private readonly ISupportConversationService _service;

    public SupportController(ISupportConversationService service) => _service = service;

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<SupportConversationDto>>> GetConversations(CancellationToken cancellationToken) =>
        Ok(await _service.GetConversationsAsync(cancellationToken));

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<SupportConversationDetailsDto>> GetConversation(Guid conversationId, CancellationToken cancellationToken) =>
        Ok(await _service.GetConversationAsync(conversationId, cancellationToken));

    [HttpPost("conversations")]
    public async Task<ActionResult<SupportConversationDetailsDto>> CreateConversation(
        [FromBody] CreateSupportConversationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateConversationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetConversation), new { conversationId = result.ConversationId }, result);
    }

    [RequestSizeLimit(6 * 1024 * 1024)]
    [HttpPost("requests")]
    public async Task<ActionResult<SupportConversationDetailsDto>> CreateEmailRequest(
        [FromForm] CreateSupportEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateEmailRequestAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetConversation), new { conversationId = result.ConversationId }, result);
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<SupportMessageDto>> AddMessage(
        Guid conversationId,
        [FromBody] AddSupportMessageRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.AddMessageAsync(conversationId, request, cancellationToken));

    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid conversationId, CancellationToken cancellationToken)
    {
        await _service.MarkReadAsync(conversationId, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Admin,SupportAdmin")]
    [HttpPut("conversations/{conversationId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid conversationId,
        [FromBody] UpdateSupportStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _service.UpdateStatusAsync(conversationId, request.Status, cancellationToken);
        return NoContent();
    }
}
