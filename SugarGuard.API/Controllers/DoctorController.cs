using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Enums;
using System.Security.Claims;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Кабинет врача
/// </summary>
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[ApiController]
[Route("api/doctor")]
[Produces("application/json")]
public class DoctorController : ControllerBase
{
    private readonly IDoctorDashboardService _dashboard;
    private readonly IDoctorNoteService _noteService;
    private readonly IChildAccessService _childAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditService _auditService;
    private readonly ILogger<DoctorController> _logger;

    /// <summary>
    /// Инициализирует контроллер кабинета врача
    /// </summary>
    public DoctorController(
        IDoctorDashboardService dashboard,
        IDoctorNoteService noteService,
        IChildAccessService childAccess,
        ICurrentUserContext currentUser,
        IAuditService auditService,
        ILogger<DoctorController> logger)
    {
        _dashboard = dashboard;
        _noteService = noteService;
        _childAccess = childAccess;
        _currentUser = currentUser;
        _auditService = auditService;
        _logger = logger;
    }

    // GET api/doctor/patients
    /// <summary>
    /// Возвращает список пациентов врача с KPI
    /// </summary>

    [HttpGet("patients")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorPatientSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPatientsAsync(
        [FromQuery] string? sortBy,
        CancellationToken cancellationToken)
    {
        var doctorUserId = _currentUser.GetUserId();

        if (doctorUserId is null)
        {
            return Unauthorized(new { error = "unauthorized", message = "Не удалось определить пользователя." });
        }

        try
        {
            var patients = await _dashboard.GetPatientsAsync(
                doctorUserId.Value, sortBy, cancellationToken);

            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetPatients: ошибка. DoctorId={DoctorId} SortBy={SortBy}.",
                doctorUserId, sortBy);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить список пациентов." });
        }
    }

    // GET api/doctor/patients/{childId}/notes
    /// <summary>
    /// Возвращает постраничный список заметок по пациенту
    /// </summary>
    [HttpGet("patients/{childId:guid}/notes")]
    [ProducesResponseType(typeof(PagedResult<DoctorNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetNotesAsync(
        Guid childId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool onlyImportant = false,
        CancellationToken cancellationToken = default)
    {
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
        {
            _logger.LogWarning(
                "GetNotes: доступ запрещён. UserId={UserId} ChildId={ChildId}.",
                _currentUser.GetUserId(), childId);

            return Forbid();
        }

        try
        {
            var result = await _noteService.GetByChildAsync(
                childId,
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 100),
                onlyImportant,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetNotes: ошибка. ChildId={ChildId} Page={Page} PageSize={PageSize}.",
                childId, page, pageSize);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить заметки." });
        }
    }

    // GET api/doctor/notes/by-measurement/{measurementId}
    /// <summary>
    /// Возвращает все заметки, привязанные к конкретному измерению
    /// </summary>
    [HttpGet("notes/by-measurement/{measurementId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetNotesByMeasurementAsync(
        Guid measurementId,
        [FromQuery] Guid childId,
        CancellationToken cancellationToken)
    {
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
        {
            _logger.LogWarning(
                "GetNotesByMeasurement: доступ запрещён. UserId={UserId} ChildId={ChildId}.",
                _currentUser.GetUserId(), childId);

            return Forbid();
        }

        try
        {
            var notes = await _noteService.GetByMeasurementAsync(
                childId, measurementId, cancellationToken);

            return Ok(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetNotesByMeasurement: ошибка. MeasurementId={MeasurementId}.", measurementId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить заметки по измерению." });
        }
    }

    // GET api/doctor/notes/{noteId}
    /// <summary>
    /// Возвращает заметку по её ID
    /// </summary>
    [HttpGet("notes/{noteId:guid}")]
    [ProducesResponseType(typeof(DoctorNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetNoteByIdAsync(
        Guid noteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var note = await _noteService.GetByIdAsync(noteId, cancellationToken);

            if (note is null)
            {
                return NotFound(new { error = "not_found", message = "Заметка не найдена." });
            }

            // IDOR-проверка: пользователь должен иметь доступ к пациенту этой заметки
            if (!await _childAccess.CanAccessChildAsync(note.ChildId, cancellationToken))
            {
                _logger.LogWarning(
                    "GetNoteById: IDOR-проверка провалена. UserId={UserId} NoteId={NoteId} ChildId={ChildId}.",
                    _currentUser.GetUserId(), noteId, note.ChildId);

                return Forbid();
            }

            return Ok(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetNoteById: ошибка. NoteId={NoteId}.", noteId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить заметку." });
        }
    }

    // POST api/doctor/notes
    /// <summary>
    /// Создаёт заметку врача к пациенту
    /// </summary>
    [HttpPost("notes")]
    [Authorize(Policy = "DoctorOrAdmin")]
    [ProducesResponseType(typeof(DoctorNoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateNoteAsync(
        [FromBody] CreateDoctorNoteRequest request,
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

        var doctorUserId = _currentUser.GetUserId();

        if (doctorUserId is null)
        {
            return Unauthorized(new { error = "unauthorized", message = "Не удалось определить пользователя." });
        }

        try
        {
            var note = await _noteService.CreateAsync(
                doctorUserId.Value, request, cancellationToken);

            await _auditService.WriteAsync(
                action: "doctornote.created",
                targetType: "DoctorNote",
                targetId: note.NoteId.ToString(),
                details: $"ChildId={note.ChildId} MeasurementId={note.MeasurementId} IsImportant={note.IsImportant}",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "CreateNote: заметка создана. NoteId={NoteId} DoctorId={DoctorId} ChildId={ChildId}.",
                note.NoteId, doctorUserId.Value, note.ChildId);

            return Created($"/api/doctor/notes/{note.NoteId:D}", note);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "CreateNote: нет связи врач–пациент. DoctorId={DoctorId} ChildId={ChildId}.",
                doctorUserId, request.ChildId);

            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = "not_found", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CreateNote: ошибка. DoctorId={DoctorId} ChildId={ChildId}.",
                doctorUserId, request.ChildId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось сохранить заметку." });
        }
    }

    // PUT api/doctor/notes/{noteId}
    /// <summary>
    /// Редактирует существующую заметку
    /// </summary>
    [HttpPut("notes/{noteId:guid}")]
    [Authorize(Policy = "DoctorOrAdmin")]
    [ProducesResponseType(typeof(DoctorNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateNoteAsync(
        Guid noteId,
        [FromBody] UpdateDoctorNoteRequest request,
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

        var doctorUserId = _currentUser.GetUserId();

        if (doctorUserId is null)
        {
            return Unauthorized(new { error = "unauthorized", message = "Не удалось определить пользователя." });
        }

        try
        {
            var updated = await _noteService.UpdateAsync(
                noteId, doctorUserId.Value, request, cancellationToken);

            if (updated is null)
            {
                return NotFound(new { error = "not_found", message = "Заметка не найдена." });
            }

            await _auditService.WriteAsync(
                action: "doctornote.updated",
                targetType: "DoctorNote",
                targetId: noteId.ToString(),
                details: $"IsImportant={updated.IsImportant}",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "UpdateNote: заметка обновлена. NoteId={NoteId} DoctorId={DoctorId}.",
                noteId, doctorUserId.Value);

            return Ok(updated);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "UpdateNote: только автор может редактировать. DoctorId={DoctorId} NoteId={NoteId}.",
                doctorUserId, noteId);

            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "UpdateNote: ошибка. NoteId={NoteId} DoctorId={DoctorId}.",
                noteId, doctorUserId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось обновить заметку." });
        }
    }

    // DELETE api/doctor/notes/{noteId}
    /// <summary>
    /// Удаляет заметку
    /// </summary>
    [HttpDelete("notes/{noteId:guid}")]
    [Authorize(Policy = "DoctorOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteNoteAsync(
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.GetUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { error = "unauthorized", message = "Не удалось определить пользователя." });
        }

        // Admin/SupportAdmin могут удалять любую заметку
        var isAdmin = User.IsInRole(UserRole.Admin.ToString())
                   || User.IsInRole(UserRole.SupportAdmin.ToString());

        try
        {
            var deleted = await _noteService.DeleteAsync(
                noteId, currentUserId.Value, isAdmin, cancellationToken);

            if (!deleted)
            {
                return NotFound(new { error = "not_found", message = "Заметка не найдена." });
            }

            await _auditService.WriteAsync(
                action: "doctornote.deleted",
                targetType: "DoctorNote",
                targetId: noteId.ToString(),
                details: $"DeletedBy={currentUserId.Value} IsAdmin={isAdmin}",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "DeleteNote: заметка удалена. NoteId={NoteId} By={By} IsAdmin={IsAdmin}.",
                noteId, currentUserId.Value, isAdmin);

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "DeleteNote: нет прав на удаление. UserId={UserId} NoteId={NoteId}.",
                currentUserId, noteId);

            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DeleteNote: ошибка. NoteId={NoteId} UserId={UserId}.",
                noteId, currentUserId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось удалить заметку." });
        }
    }

    // GET api/doctor/cohort/summary
    /// <summary>
    /// Возвращает агрегированную сводку по группе пациентов врача
    /// </summary>
    [HttpGet("cohort/summary")]
    [Authorize(Policy = "DoctorOrAdmin")]
    [ProducesResponseType(typeof(DoctorCohortSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCohortSummaryAsync(
        CancellationToken cancellationToken)
    {
        var doctorUserId = _currentUser.GetUserId();

        if (doctorUserId is null)
        {
            return Unauthorized(new { error = "unauthorized", message = "Не удалось определить пользователя." });
        }

        try
        {
            var summary = await _dashboard.GetCohortSummaryAsync(
                doctorUserId.Value, cancellationToken);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetCohortSummary: ошибка. DoctorId={DoctorId}.", doctorUserId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить сводку когорты." });
        }
    }
}
