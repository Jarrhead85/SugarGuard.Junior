using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Services;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Контроллер для привязки Telegram-пользователя к ребёнку через одноразовый код
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ParentLinkController : ControllerBase
{
    private readonly IParentLinkService _parentLink;
    private readonly IChildAccessService _childAccess;
    private readonly ILogger<ParentLinkController> _logger;

    public ParentLinkController(
        IParentLinkService parentLink,
        IChildAccessService childAccess,
        ILogger<ParentLinkController> logger)
    {
        _parentLink = parentLink;
        _childAccess = childAccess;
        _logger = logger;
    }

    // POST api/parent-link/code
    /// <summary>
    /// Создаёт новый ConnectionCode для ребёнка
    /// </summary>
    [Authorize(Policy = "ParentOrDoctorOrAdmin")]
    [HttpPost("code")]
    [ProducesResponseType(typeof(SaveConnectionCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SaveConnectionCodeResponse>> SaveConnectionCode(
        [FromBody] SaveConnectionCodeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                error = "validation_error",
                details = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
            });

        try
        {
            if (!await _childAccess.CanAccessChildAsync(request.ChildId, cancellationToken))
            {
                _logger.LogWarning(
                    "SaveConnectionCode: доступ запрещён. UserId={UserId} ChildId={ChildId}.",
                    User.Identity?.Name ?? "unknown", request.ChildId);

                return Forbid();
            }

            var result = await _parentLink.SaveConnectionCodeAsync(request, cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new SaveConnectionCodeResponse
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage ?? "Child not found"
                });
            }

            return Ok(new SaveConnectionCodeResponse
            {
                Success = true,
                CodeId = result.CodeId ?? Guid.Empty,
                ExpiresAt = result.ExpiresAt ?? DateTime.MinValue
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveConnectionCode: внутренняя ошибка. ChildId={ChildId}.", request.ChildId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    // POST api/parent-link/verify
    /// <summary>
    /// Верифицирует ConnectionCode и привязывает Telegram-пользователя к ребёнку
    /// </summary>
    [Authorize(Roles = "ServiceAccount")]
    [HttpPost("verify")]
    [ProducesResponseType(typeof(VerifyConnectionCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<VerifyConnectionCodeResponse>> VerifyConnectionCode(
        [FromBody] VerifyConnectionCodeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                error = "validation_error",
                details = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
            });

        try
        {
            var result = await _parentLink.VerifyConnectionCodeAsync(request, cancellationToken);

            return Ok(new VerifyConnectionCodeResponse
            {
                Success = result.Success,
                IsValid = result.IsValid,
                ChildId = result.ChildId,
                LinkId = result.LinkId,
                Message = result.Message,
                ErrorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "VerifyConnectionCode: внутренняя ошибка. TelegramUserId={TelegramUserId}.",
                request.TelegramUserId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }
}
