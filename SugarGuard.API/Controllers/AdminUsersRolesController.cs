using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Services;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Dto;

namespace SugarGuard.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/users-roles")]
public class AdminUsersRolesController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ICurrentUserContext _currentUser;

    public AdminUsersRolesController(IAdminService adminService, ICurrentUserContext currentUser)
    {
        _adminService = adminService;
        _currentUser = currentUser;
    }

    // GET api/admin/users-roles/users
    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserResponse>>> GetUsers(
        [FromQuery] UserRole? role,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetUsersAsync(role, limit, cancellationToken);
        return Ok(result);
    }

    // PUT api/admin/users-roles/users/{userId}/role
    [HttpPut("users/{userId:guid}/role")]
    [Authorize(Policy = "FullAdminOnly")]
    public async Task<ActionResult<AdminUserResponse>> UpdateRole(
        Guid userId,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.GetUserId() == userId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "self_role_change_forbidden",
                message = "Нельзя изменять собственную роль."
            });
        }

        AdminUserResponse? result;
        try
        {
            result = await _adminService.UpdateUserRoleAsync(userId, request.NewRole, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "invalid_role", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "role_change_not_allowed", message = ex.Message });
        }

        return result is null
            ? NotFound(new { error = "user_not_found", message = "User not found" })
            : Ok(result);
    }

    // POST api/admin/users-roles/links/parent-child
    [HttpPost("links/parent-child")]
    public async Task<IActionResult> CreateParentChildLink(
        [FromBody] CreateParentChildLinkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _adminService.CreateParentChildLinkAsync(
                request.ParentUserId, request.ChildId, cancellationToken);

            return Ok(new { success = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "invalid_ids", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "link_exists", message = ex.Message });
        }
    }

    // DELETE api/admin/users-roles/links/parent-child
    [HttpDelete("links/parent-child")]
    public async Task<IActionResult> RemoveParentChildLink(
        [FromQuery] Guid parentUserId,
        [FromQuery] Guid childId,
        CancellationToken cancellationToken)
    {
        var removed = await _adminService.RemoveParentChildLinkAsync(
            parentUserId, childId, cancellationToken);

        return removed
            ? NoContent()
            : NotFound(new { error = "link_not_found", message = "Link not found" });
    }

    // POST api/admin/users-roles/links/doctor-child
    [HttpPost("links/doctor-child")]
    public async Task<IActionResult> CreateDoctorChildLink(
        [FromBody] CreateDoctorChildLinkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _adminService.CreateDoctorChildLinkAsync(
                request.DoctorUserId, request.ChildId, cancellationToken);

            return Ok(new { success = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "invalid_ids", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "link_exists", message = ex.Message });
        }
    }

        // DELETE api/admin/users-roles/links/doctor-child
        [HttpDelete("links/doctor-child")]
        public async Task<IActionResult> RemoveDoctorChildLink(
            [FromQuery] Guid doctorUserId,
            [FromQuery] Guid childId,
            CancellationToken cancellationToken)
        {
            var removed = await _adminService.RemoveDoctorChildLinkAsync(
                doctorUserId, childId, cancellationToken);

            return removed
                ? NoContent()
                : NotFound(new { error = "link_not_found", message = "Link not found" });
        }

        // GET api/admin/users-roles/links/parent-child
        [HttpGet("links/parent-child")]
        [ProducesResponseType(typeof(IReadOnlyList<AdminParentChildLinkResponse>),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<AdminParentChildLinkResponse>>> ListParentChildLinks(
            CancellationToken cancellationToken)
        {
            var items = await _adminService.GetAllParentChildLinksAsync(cancellationToken);
            return Ok(items);
        }

        // GET api/admin/users-roles/links/doctor-child
        [HttpGet("links/doctor-child")]
        [ProducesResponseType(typeof(IReadOnlyList<AdminDoctorChildLinkResponse>),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<AdminDoctorChildLinkResponse>>> ListDoctorChildLinks(
            CancellationToken cancellationToken)
        {
            var items = await _adminService.GetAllDoctorChildLinksAsync(cancellationToken);
            return Ok(items);
        }
}
