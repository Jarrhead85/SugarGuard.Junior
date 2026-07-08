using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;

namespace SugarGuard.API.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountProfileService _profiles;
    private readonly ICurrentUserContext _currentUser;

    public AccountController(IAccountProfileService profiles, ICurrentUserContext currentUser)
    {
        _profiles = profiles;
        _currentUser = currentUser;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<AccountProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var profile = await _profiles.GetAsync(userId.Value, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<AccountProfileResponse>> UpdateProfile(
        UpdateAccountProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var profile = await _profiles.UpdateAsync(userId.Value, request, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost("profile/photo")]
    [RequestSizeLimit(5L * 1024 * 1024)]
    public async Task<ActionResult<AccountPhotoUploadResponse>> UploadPhoto(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var photoUrl = await _profiles.UploadPhotoAsync(userId.Value, file, cancellationToken);
            return Ok(new AccountPhotoUploadResponse { PhotoUrl = photoUrl });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = "invalid_photo", message = exception.Message });
        }
    }

    [HttpDelete("profile/photo")]
    public async Task<IActionResult> DeletePhoto(CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        return await _profiles.DeletePhotoAsync(userId.Value, cancellationToken) ? NoContent() : NotFound();
    }
}
