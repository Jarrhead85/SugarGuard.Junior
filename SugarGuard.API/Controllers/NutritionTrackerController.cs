using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Controllers;

[ApiController]
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[Route("api/children/{childId:guid}/nutrition")]
public sealed class NutritionTrackerController : ControllerBase
{
    private readonly INutritionTrackerService _tracker;
    private readonly IChildAccessService _childAccess;
    private readonly ICurrentUserContext _currentUser;

    public NutritionTrackerController(INutritionTrackerService tracker, IChildAccessService childAccess, ICurrentUserContext currentUser)
    {
        _tracker = tracker;
        _childAccess = childAccess;
        _currentUser = currentUser;
    }

    [HttpGet("entries")]
    public async Task<ActionResult<IReadOnlyList<NutritionEntryResponse>>> GetEntries(Guid childId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
    {
        if (!TryValidatePeriod(from, to, out var error)) return BadRequest(new { error });
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        return Ok(await _tracker.GetEntriesAsync(childId, NormalizeQueryTime(from), NormalizeQueryTime(to), cancellationToken));
    }

    [HttpPost("entries")]
    public async Task<ActionResult<NutritionEntryResponse>> CreateEntry(Guid childId, SaveNutritionEntryRequest request, CancellationToken cancellationToken)
    {
        if (!CanEdit()) return Forbid();
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        var actorId = _currentUser.GetUserId();
        if (!actorId.HasValue) return Unauthorized();
        var result = await _tracker.CreateEntryAsync(childId, actorId.Value, request, cancellationToken);
        return CreatedAtAction(nameof(GetEntries), new { childId, from = result.RecordedAt.Date, to = result.RecordedAt.Date.AddDays(1) }, result);
    }

    [HttpPut("entries/{entryId:guid}")]
    public async Task<ActionResult<NutritionEntryResponse>> UpdateEntry(Guid childId, Guid entryId, SaveNutritionEntryRequest request, CancellationToken cancellationToken)
    {
        if (!CanEdit()) return Forbid();
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        var result = await _tracker.UpdateEntryAsync(childId, entryId, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid childId, Guid entryId, CancellationToken cancellationToken)
    {
        if (!CanEdit()) return Forbid();
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        return await _tracker.DeleteEntryAsync(childId, entryId, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpGet("schedule")]
    public async Task<ActionResult<IReadOnlyList<MealScheduleResponse>>> GetSchedule(Guid childId, CancellationToken cancellationToken)
    {
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        return Ok(await _tracker.GetSchedulesAsync(childId, cancellationToken));
    }

    [HttpPost("schedule")]
    public async Task<ActionResult<MealScheduleResponse>> CreateSchedule(Guid childId, SaveMealScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!CanEdit()) return Forbid();
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        var result = await _tracker.CreateScheduleAsync(childId, request, cancellationToken);
        return CreatedAtAction(nameof(GetSchedule), new { childId }, result);
    }

    [HttpPut("schedule/{scheduleId:guid}")]
    public async Task<ActionResult<MealScheduleResponse>> UpdateSchedule(Guid childId, Guid scheduleId, SaveMealScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!CanEdit()) return Forbid();
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        var result = await _tracker.UpdateScheduleAsync(childId, scheduleId, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("schedule/{scheduleId:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid childId, Guid scheduleId, CancellationToken cancellationToken)
    {
        if (!CanEdit()) return Forbid();
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        return await _tracker.DeleteScheduleAsync(childId, scheduleId, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<NutritionSummaryResponse>> GetSummary(Guid childId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
    {
        if (!TryValidatePeriod(from, to, out var error)) return BadRequest(new { error });
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        return Ok(await _tracker.GetSummaryAsync(childId, NormalizeQueryTime(from), NormalizeQueryTime(to), cancellationToken));
    }

    [HttpGet("achievements")]
    public async Task<ActionResult<IReadOnlyList<AchievementResponse>>> GetAchievements(Guid childId, CancellationToken cancellationToken)
    {
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        return Ok(await _tracker.GetAchievementsAsync(childId, cancellationToken));
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(Guid childId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
    {
        if (!TryValidatePeriod(from, to, out var error)) return BadRequest(new { error });
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        // UTF-8 с BOM и тип CSV сохраняют кодировку при скачивании через браузер.
        return File(
            await _tracker.ExportCsvAsync(childId, NormalizeQueryTime(from), NormalizeQueryTime(to), cancellationToken),
            "text/csv; charset=utf-8",
            $"nutrition-{from:yyyyMMdd}-{to:yyyyMMdd}-utf8.csv");
    }

    [HttpGet("export.pdf")]
    public async Task<IActionResult> ExportPdf(Guid childId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
    {
        if (!TryValidatePeriod(from, to, out var error)) return BadRequest(new { error });
        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken)) return Forbid();
        return File(await _tracker.ExportPdfAsync(childId, NormalizeQueryTime(from), NormalizeQueryTime(to), cancellationToken), "application/pdf", $"nutrition-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf");
    }

    private bool CanEdit() => _currentUser.GetRole() is UserRole.Parent or UserRole.ChildDevice or UserRole.Admin or UserRole.SupportAdmin;

    private static bool TryValidatePeriod(DateTime from, DateTime to, out string? error)
    {
        if (from == default || to == default || from > to) { error = "Некорректный период."; return false; }
        if (to - from > TimeSpan.FromDays(366)) { error = "Период не может превышать 366 дней."; return false; }
        error = null; return true;
    }

    private static DateTime NormalizeQueryTime(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => TimeZoneInfo.ConvertTimeToUtc(value, DefaultTimeZone)
    };

    private static TimeZoneInfo DefaultTimeZone
    {
        get
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
            }
        }
    }
}
