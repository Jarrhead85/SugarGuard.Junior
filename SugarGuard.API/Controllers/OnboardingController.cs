using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Dto;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Управляет онбордингом текущего пользователя
/// </summary>
[Authorize]
[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboarding;
    private readonly IChildrenService _childrenService;
    private readonly IChildAccessService _childAccess;
    private readonly ILogger<OnboardingController> _logger;

    /// <summary>
    /// Конструктор с DI
    /// </summary>
    public OnboardingController(
        IOnboardingService onboarding,
        IChildrenService childrenService,
        IChildAccessService childAccess,
        ILogger<OnboardingController> logger)
    {
        _onboarding = onboarding;
        _childrenService = childrenService;
        _childAccess = childAccess;
        _logger = logger;
    }

    // POST /api/onboarding/child
    /// <summary>
    /// Создаёт профиль ребёнка в онбординге
    /// </summary>
    [HttpPost("child")]
    [ProducesResponseType(typeof(CreateChildOnboardingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CreateChildOnboardingResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateChildOnboardingResponse>> CreateChildOnboardingAsync(
        [FromBody] CreateChildOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new CreateChildOnboardingResponse
            {
                Success = false,
                ErrorMessage = "Невалидные данные запроса."
            });
        }

        var userId = _childAccess.GetCurrentUserId();
        var role = _childAccess.GetCurrentUserRole();

        if (!userId.HasValue || !role.HasValue)
        {
            return Unauthorized(new CreateChildOnboardingResponse
            {
                Success = false,
                ErrorMessage = "Пользователь не аутентифицирован."
            });
        }

        if (role.Value is not (UserRole.Parent
                               or UserRole.Admin
                               or UserRole.SupportAdmin
                               or UserRole.ServiceAccount))
        {
            _logger.LogWarning(
                "CreateChildOnboarding: попытка создания ребёнка запрещённой ролью. " +
                "UserId={UserId} Role={Role}.",
                userId, role.Value);

            return StatusCode(StatusCodes.Status403Forbidden, new CreateChildOnboardingResponse
            {
                Success = false,
                ErrorMessage = "Создание профиля ребёнка доступно только родителю."
            });
        }

        try
        {
            var createRequest = new CreateChildRequest
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                DiabetesType = request.DiabetesType,
                DiagnosisDate = request.DiagnosisDate,
                Weight = request.Weight,
                Height = request.Height,
                TimeZoneId = request.TimeZoneId,
                InsulinScheme = request.InsulinScheme
            };

            var result = await _childrenService.CreateAsync(
                userId.Value, role.Value, createRequest, cancellationToken);

            return Ok(new CreateChildOnboardingResponse
            {
                Success = true,
                ChildId = result.Child.ChildId,
                LinkId = result.ParentLinkId,
                NextStep = OnboardingStep.DiabetesSettings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateChildOnboarding: ошибка для UserId {UserId}.", userId);
            return StatusCode(500, new CreateChildOnboardingResponse
            {
                Success = false,
                ErrorMessage = "Не удалось создать профиль ребёнка."
            });
        }
    }

    // GET /api/onboarding/status
    /// <summary>
    /// Возвращает текущий статус онбординга авторизованного пользователя
    /// </summary>

    [HttpGet("status")]
    [ProducesResponseType(typeof(OnboardingStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnboardingStatusResponse>> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        var userId = _childAccess.GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(new { error = "unauthorized", message = "Пользователь не аутентифицирован." });

        try
        {
            var result = await _onboarding.GetStatusAsync(userId.Value, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "usernotfound", message = "Пользователь не найден." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOnboardingStatus: ошибка для UserId {UserId}.", userId.Value);
            return StatusCode(500, new { error = "internalerror", message = "Внутренняя ошибка сервера." });
        }
    }

    // POST /api/onboarding/steps/{step}/complete 
    /// <summary>
    /// Отмечает шаг онбординга как завершённый
    /// </summary>
    [HttpPost("steps/{step:int}/complete")]
    [ProducesResponseType(typeof(OnboardingStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnboardingStatusResponse>> CompleteStepAsync(
        [FromRoute] int step,
        CancellationToken cancellationToken)
    {
        var userId = _childAccess.GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(new { error = "unauthorized", message = "Пользователь не аутентифицирован." });

        try
        {
            var result = await _onboarding.CompleteStepAsync(userId.Value, step, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "usernotfound", message = "Пользователь не найден." });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = "invalidstep", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CompleteStep: ошибка для UserId {UserId}, шаг {Step}.", userId.Value, step);
            return StatusCode(500, new { error = "internalerror", message = "Внутренняя ошибка сервера." });
        }
    }

    // POST /api/onboarding/skip

    /// <summary>
    /// Пропускает онбординг
    /// </summary>
    [HttpPost("skip")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SkipAsync(CancellationToken cancellationToken)
    {
        var userId = _childAccess.GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(new { error = "unauthorized", message = "Пользователь не аутентифицирован." });

        try
        {
            await _onboarding.SkipAsync(userId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "usernotfound", message = "Пользователь не найден." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SkipOnboarding: ошибка для UserId {UserId}.", userId.Value);
            return StatusCode(500, new { error = "internalerror", message = "Внутренняя ошибка сервера." });
        }
    }
}
