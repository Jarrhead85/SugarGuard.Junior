using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Controllers;

[ApiController]
[Route("api/doctor-verification")]
public sealed class DoctorVerificationController : ControllerBase
{
    private readonly IDoctorVerificationService _verification;
    private readonly ICurrentUserContext _currentUser;

    public DoctorVerificationController(IDoctorVerificationService verification, ICurrentUserContext currentUser)
    {
        _verification = verification;
        _currentUser = currentUser;
    }

    [HttpGet("mine")]
    [Authorize(Roles = Roles.DoctorPending)]
    public async Task<ActionResult<DoctorVerificationResponse>> GetMine(CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        return !userId.HasValue ? Unauthorized() : (await _verification.GetMineAsync(userId.Value, cancellationToken) is { } request ? Ok(request) : NotFound());
    }

    [HttpPost("submit")]
    [Authorize(Roles = Roles.DoctorPending)]
    [RequestSizeLimit(30L * 1024 * 1024)]
    public async Task<ActionResult<DoctorVerificationResponse>> Submit(
        [FromForm] SubmitDoctorVerificationRequest request,
        [FromForm] List<IFormFile> documents,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (!userId.HasValue) return Unauthorized();
        try
        {
            return Ok(await _verification.SubmitAsync(userId.Value, request, documents, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = "invalid_verification_request", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = "verification_request_conflict", message = exception.Message });
        }
    }

    [HttpGet("admin/pending")]
    [Authorize(Policy = "FullAdminOnly")]
    public async Task<ActionResult<IReadOnlyList<AdminDoctorVerificationResponse>>> GetPending(CancellationToken cancellationToken)
        => Ok(await _verification.GetPendingAsync(cancellationToken));

    [HttpPost("admin/{requestId:guid}/approve")]
    [Authorize(Policy = "FullAdminOnly")]
    public async Task<ActionResult<AdminDoctorVerificationResponse>> Approve(Guid requestId, [FromBody] ReviewDoctorVerificationRequest request, CancellationToken cancellationToken)
        => await ReviewAsync(requestId, request.Comment, approved: true, cancellationToken);

    [HttpPost("admin/{requestId:guid}/reject")]
    [Authorize(Policy = "FullAdminOnly")]
    public async Task<ActionResult<AdminDoctorVerificationResponse>> Reject(Guid requestId, [FromBody] ReviewDoctorVerificationRequest request, CancellationToken cancellationToken)
        => await ReviewAsync(requestId, request.Comment, approved: false, cancellationToken);

    [HttpGet("admin/documents/{documentId:guid}")]
    [Authorize(Policy = "FullAdminOnly")]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken cancellationToken)
    {
        var file = await _verification.OpenDocumentAsync(documentId, cancellationToken);
        return file is null ? NotFound() : File(file.Value.Stream, file.Value.ContentType, file.Value.FileName, enableRangeProcessing: true);
    }

    private async Task<ActionResult<AdminDoctorVerificationResponse>> ReviewAsync(Guid requestId, string? comment, bool approved, CancellationToken cancellationToken)
    {
        var reviewerId = _currentUser.GetUserId();
        if (!reviewerId.HasValue) return Unauthorized();
        try
        {
            var result = approved
                ? await _verification.ApproveAsync(requestId, reviewerId.Value, comment, cancellationToken)
                : await _verification.RejectAsync(requestId, reviewerId.Value, comment ?? string.Empty, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = "invalid_review", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = "verification_request_conflict", message = exception.Message });
        }
    }
}
