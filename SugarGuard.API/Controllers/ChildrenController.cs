using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Services;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Controllers;

/// <summary>
/// CRUD-операции над профилями детей
/// </summary>
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[ApiController]
[Route("api/children")]
[Produces("application/json")]
public class ChildrenController : ControllerBase
{
    private const string UploadsBaseUrl = "/uploads/children";

    private readonly IChildrenService _childrenService;
    private readonly IDiabetesSettingsService _diabetesSettings;
    private readonly IChildAccessService _childAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ChildrenController> _logger;

    public ChildrenController(
        IChildrenService childrenService,
        IDiabetesSettingsService diabetesSettings,
        IChildAccessService childAccess,
        ICurrentUserContext currentUser,
        IWebHostEnvironment env,
        ILogger<ChildrenController> logger)
    {
        _childrenService = childrenService;
        _diabetesSettings = diabetesSettings;
        _childAccess = childAccess;
        _currentUser = currentUser;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Страница списка детей, доступных текущему пользователю
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ChildSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetChildren(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return BadRequest(new
            {
                error = "validation_error",
                message = "page должен быть >= 1."
            });
        }

        if (pageSize < 1 || pageSize > 200)
        {
            return BadRequest(new
            {
                error = "validation_error",
                message = "pageSize должен быть в диапазоне 1..200."
            });
        }

        var userId = _currentUser.GetUserId();
        var role = _currentUser.GetRole();

        if (!userId.HasValue || !role.HasValue)
            return Unauthorized(new { error = "unauthorized", message = "Пользователь не авторизован." });

        try
        {
            var children = await _childrenService.GetAccessibleAsync(
                userId.Value, role.Value, page, pageSize, cancellationToken);

            return Ok(children);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetChildren: ошибка для UserId={UserId}.", userId);
            return this.ProblemWithCode(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "Не удалось получить список детей.",
                "internal_error");
        }
    }

    /// <summary>
    /// Получить профиль ребёнка по ID
    /// </summary>
    [HttpGet("{childId:guid}")]
    [ProducesResponseType(typeof(ChildResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChild(
        Guid childId,
        CancellationToken cancellationToken)
    {
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var child = await _childrenService.GetByIdAsync(childId, cancellationToken);

            if (child is null)
            {
                return this.ProblemWithCode(404, "Child Not Found",
                    "Ребёнок не найден", "child_not_found");
            }

            return Ok(child);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetChild: ошибка. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить профиль ребёнка." });
        }
    }

    /// <summary>
    /// Создать профиль ребёнка
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChildResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateChild(
        [FromBody] CreateChildRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new { error = "validation_error", details = errors });
        }

        var userId = _currentUser.GetUserId();
        var role = _currentUser.GetRole();

        if (!userId.HasValue || !role.HasValue)
            return Unauthorized(new { error = "unauthorized", message = "Пользователь не авторизован." });

        try
        {
            var result = await _childrenService.CreateAsync(
                userId.Value, role.Value, request, cancellationToken);

            _logger.LogInformation(
                "CreateChild: ChildId={ChildId} UserId={UserId} Role={Role}.",
                result.Child.ChildId, userId, role);

            return CreatedAtAction(
                nameof(GetChild),
                new { childId = result.Child.ChildId },
                result.Child);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateChild: ошибка. UserId={UserId}.", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось создать профиль ребёнка." });
        }
    }

    /// <summary>
    /// Обновить профиль ребёнка
    /// </summary>
    [HttpPut("{childId:guid}")]
    [ProducesResponseType(typeof(ChildResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateChild(
        Guid childId,
        [FromBody] UpdateChildRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new { error = "validation_error", details = errors });
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var child = await _childrenService.UpdateAsync(childId, request, cancellationToken);

            if (child is null)
            {
                return this.ProblemWithCode(404, "Child Not Found",
                    "Ребёнок не найден", "child_not_found");
            }

            return Ok(child);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateChild: ошибка. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось обновить профиль ребёнка." });
        }
    }

    /// <summary>
    /// Получить медицинские настройки диабета ребёнка
    /// </summary>
    [HttpGet("{childId:guid}/diabetes-settings")]
    [ProducesResponseType(typeof(DiabetesSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDiabetesSettings(
        Guid childId,
        CancellationToken cancellationToken)
    {
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var settings = await _diabetesSettings.GetAsync(childId, cancellationToken);
            if (settings is null)
            {
                return this.ProblemWithCode(404, "Diabetes Settings Not Found",
                    "Настройки диабета ещё не заданы для этого ребёнка.",
                    "diabetes_settings_not_found");
            }

            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDiabetesSettings: ошибка. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить настройки диабета." });
        }
    }

    /// <summary>
    /// Создать или обновить медицинские настройки диабета ребёнка
    /// </summary>
    [HttpPatch("{childId:guid}/diabetes-settings")]
    [ProducesResponseType(typeof(DiabetesSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertDiabetesSettings(
        Guid childId,
        [FromBody] UpdateDiabetesSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new { error = "validation_error", details = errors });
        }

        // Бизнес-правило: target min должен быть строго меньше max.
        if (request.TargetRangeMin >= request.TargetRangeMax)
        {
            return BadRequest(new
            {
                error = "validation_error",
                message = "TargetRangeMin должен быть строго меньше TargetRangeMax."
            });
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var existed = await _diabetesSettings.GetAsync(childId, cancellationToken);
            var settings = await _diabetesSettings.UpsertAsync(childId, request, cancellationToken);

            if (settings is null)
            {
                return this.ProblemWithCode(404, "Child Not Found",
                    "Ребёнок не найден", "child_not_found");
            }

            _logger.LogInformation(
                "UpsertDiabetesSettings: ChildId={ChildId} {Action}.",
                childId, existed is null ? "created" : "updated");

            return existed is null
                ? StatusCode(StatusCodes.Status201Created, settings)
                : Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertDiabetesSettings: ошибка. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось обновить настройки диабета." });
        }
    }

    /// <summary>
    /// Удаляет ребёнка. Только Admin.
    /// </summary>
    [HttpDelete("{childId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteChild(
        Guid childId,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _childrenService.DeleteChildAsync(childId, cancellationToken);

            if (!deleted)
                return this.ProblemWithCode(404, "Child Not Found",
                    "Ребёнок не найден", "child_not_found");

            _logger.LogInformation("DeleteChild: ChildId={ChildId} deleted.", childId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteChild: ошибка. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось удалить ребёнка." });
        }
    }

    /// <summary>
    /// Загружает фото профиля ребёнка
    /// </summary>
    [HttpPost("{childId:guid}/photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)] // 6 МБ — фото до 5 МБ + form overhead
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ChildPhotoUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadPhoto(
        Guid childId,
        IFormFile photo,
        CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { error = "validation_error", message = "Файл не передан или пуст." });

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var wwwroot = _env.WebRootPath
                ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            var photoUrl = await _childrenService.UploadPhotoAsync(
                childId, photo, wwwroot, UploadsBaseUrl, cancellationToken);

            if (photoUrl is null)
            {
                return this.ProblemWithCode(404, "Child Not Found",
                    "Ребёнок не найден", "child_not_found");
            }

            _logger.LogInformation("UploadPhoto: ChildId={ChildId} PhotoUrl={PhotoUrl}.",
                childId, photoUrl);

            return Ok(new ChildPhotoUploadResponse { PhotoUrl = photoUrl });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("UploadPhoto: валидация не прошла. ChildId={ChildId} {Message}",
                childId, ex.Message);
            return BadRequest(new { error = "validation_error", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UploadPhoto: ошибка. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось загрузить фото." });
        }
    }

    /// <summary>
    /// Удаляет фото профиля ребёнка
    /// </summary>
    [HttpDelete("{childId:guid}/photo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePhoto(
        Guid childId,
        CancellationToken cancellationToken)
    {
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var wwwroot = _env.WebRootPath
                ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            var deleted = await _childrenService.DeletePhotoAsync(childId, wwwroot, cancellationToken);

            if (!deleted)
            {
                return this.ProblemWithCode(404, "Photo Not Found",
                    "Фото отсутствует или ребёнок не найден", "photo_not_found");
            }

            _logger.LogInformation("DeletePhoto: ChildId={ChildId} deleted.", childId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeletePhoto: ошибка. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось удалить фото." });
        }
    }
}
