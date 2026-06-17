using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Services;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Controllers
{
    /// <summary>
    /// Управление кодами приглашений для связки ребёнка с родителем или врачом
    /// </summary>
    [ApiController]
    [Route("api/invite-codes")]
    [Produces("application/json")]
    public class InviteCodesController : ControllerBase
    {
        private readonly IInviteCodeService _inviteCodeService;
        private readonly IChildAccessService _childAccess;
        private readonly ILogger<InviteCodesController> _logger;

        public InviteCodesController(
            IInviteCodeService inviteCodeService,
            IChildAccessService childAccess,
            ILogger<InviteCodesController> logger)
        {
            _inviteCodeService = inviteCodeService;
            _childAccess = childAccess;
            _logger = logger;
        }

        // POST /api/invite-codes/generate  
        /// <summary>
        /// Генерирует новый код приглашения для ребёнка
        /// </summary>

        [Authorize]
        [HttpPost("generate")]
        [ProducesResponseType(typeof(InviteCodeResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<InviteCodeResponse>> Generate(
            [FromBody] GenerateInviteCodeRequest request,
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

            // Только админ или сам ребёнок могут генерировать коды
            if (!await _childAccess.CanAccessChildAsync(request.ChildId, cancellationToken))
                return Forbid();

            // Допустимые целевые роли: только Parent и Doctor
            if (request.TargetRole != UserRole.Parent && request.TargetRole != UserRole.Doctor)
                return BadRequest(new
                {
                    error = "invalid_role",
                    message = "TargetRole должен быть Parent или Doctor."
                });

            try
            {
                var result = await _inviteCodeService.GenerateAsync(
                    request.ChildId,
                    request.TargetRole,
                    cancellationToken);

                _logger.LogInformation(
                    "Код приглашения сгенерирован. ChildId={ChildId} Role={Role} InviteCodeId={Id}",
                    request.ChildId, request.TargetRole, result.InviteCodeId);

                return CreatedAtAction(
                    nameof(GetActive),
                    new { childId = request.ChildId },
                    result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = "invalid_argument", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Ошибка генерации кода приглашения. ChildId={ChildId}", request.ChildId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "internal_error", message = "Внутренняя ошибка сервера." });
            }
        }

        // POST /api/invite-codes/claim
        /// <summary>
        /// Принимает код приглашения
        /// </summary>
        [Authorize(Policy = "ParentOrDoctorOrAdmin")]
        [HttpPost("claim")]
        [ProducesResponseType(typeof(ClaimInviteCodeResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ClaimInviteCodeResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ClaimInviteCodeResult>> Claim(
    [FromBody] ClaimInviteCodeRequest request,
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

            var userId = _childAccess.GetCurrentUserId();
            if (!userId.HasValue)
            {
                return this.ProblemWithCode(401, "Unauthorized",
                    "Пользователь не авторизован", "unauthorized");
            }

            try
            {
                var result = await _inviteCodeService.ClaimAsync(
                    request.Code,
                    userId.Value,
                    cancellationToken);

                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Неуспешная попытка принять код. UserId={UserId} ErrorCode={Code}",
                        userId.Value, result.ErrorCode);

                    return result.ErrorCode == "access_denied"
                        ? Forbid()
                        : BadRequest(result);
                }

                _logger.LogInformation(
                    "Код принят. UserId={UserId} ChildId={ChildId} LinkType={LinkType} LinkId={LinkId}",
                    userId.Value, result.ChildId, result.LinkType, result.LinkId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Ошибка при принятии кода приглашения. UserId={UserId}", userId.Value);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "internal_error", message = "Внутренняя ошибка сервера." });
            }
        }

        // GET /api/invite-codes/{childId}/active
        /// <summary>
        /// Возвращает список активных кодов для ребёнка
        /// </summary>
        [Authorize]
        [HttpGet("{childId:guid}/active")]
        [ProducesResponseType(typeof(IReadOnlyList<InviteCodeResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<InviteCodeResponse>>> GetActive(
            Guid childId,
            CancellationToken cancellationToken)
        {
            if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
                return Forbid();

            try
            {
                var codes = await _inviteCodeService.GetActiveAsync(childId, cancellationToken);
                return Ok(codes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Ошибка получения активных кодов. ChildId={ChildId}", childId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "internal_error", message = "Внутренняя ошибка сервера." });
            }
        }

        // GET /api/invite-codes/{childId}/links
        /// <summary>
        /// Возвращает текущие связки ребёнка с родителями и врачами
        /// </summary>
        [Authorize]
        [HttpGet("{childId:guid}/links")]
        [ProducesResponseType(typeof(ChildAccessLinksResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ChildAccessLinksResponse>> GetLinks(
            Guid childId,
            CancellationToken cancellationToken)
        {
            if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
                return Forbid();

            try
            {
                var result = await _inviteCodeService.GetChildLinksAsync(childId, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Ошибка получения связок для ребёнка. ChildId={ChildId}", childId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "internal_error", message = "Внутренняя ошибка сервера." });
            }
        }

        // DELETE /api/invite-codes/{childId}/links/{linkType}/{linkId}
        /// <summary>
        /// Удаляет существующую связку ребёнка с родителем или врачом
        /// </summary>
        [Authorize]
        [HttpDelete("{childId:guid}/links/{linkType}/{linkId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unlink(
            Guid childId,
            string linkType,
            Guid linkId,
            CancellationToken cancellationToken)
        {
            if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
                return Forbid();

            try
            {
                var result = await _inviteCodeService.UnlinkAsync(
                    childId, linkType, linkId, cancellationToken);

                return result switch
                {
                    UnlinkResult.Success => NoContent(),
                    UnlinkResult.NotFound => this.ProblemWithCode(404, "Link Not Found",
                        "Связка не найдена", "link_not_found"),
                    UnlinkResult.InvalidLinkType => BadRequest(new
                    {
                        error = "invalid_link_type",
                        message = "Допустимые значения linkType: parent, doctor."
                    }),
                    _ => StatusCode(StatusCodes.Status500InternalServerError,
                        new { error = "internal_error", message = "Неожиданный результат." })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Ошибка удаления связки. ChildId={ChildId}, LinkType={LinkType}, LinkId={LinkId}",
                    childId, linkType, linkId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "internal_error", message = "Внутренняя ошибка сервера." });
            }
        }

        // DELETE /api/invite-codes/{inviteCodeId}
        /// <summary>
        /// Отзывает (аннулирует) код приглашения досрочно
        /// </summary>
        [Authorize]
        [HttpDelete("{inviteCodeId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Revoke(
            Guid inviteCodeId,
            CancellationToken cancellationToken)
        {
            try
            {
                var revoked = await _inviteCodeService.RevokeAsync(inviteCodeId, cancellationToken);

                if (!revoked)
                {
                    _logger.LogWarning(
                        "Не удалось отозвать код — не найден или уже использован. InviteCodeId={Id}",
                        inviteCodeId);

                    return Conflict(new
                    {
                        error = "revoke_failed",
                        message = "Код не найден или уже не активен."
                    });
                }

                _logger.LogInformation(
                    "Код приглашения отозван. InviteCodeId={Id}", inviteCodeId);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Ошибка отзыва кода приглашения. InviteCodeId={Id}", inviteCodeId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "internal_error", message = "Внутренняя ошибка сервера." });
            }
        }
    }
}
