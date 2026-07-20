using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Controllers;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;

namespace SugarGuard.Tests.Integration;

/// <summary>
/// Интеграционные тесты защиты изменения ролей.
/// </summary>
[Collection("ExportJobs")]
public sealed class AdminUsersRolesAuthorizationTests
{
    private readonly ExportJobsWebApplicationFactory _factory;

    public AdminUsersRolesAuthorizationTests(ExportJobsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateRole_SupportAdmin_IsForbidden()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(Guid.NewGuid(), "SupportAdmin"));

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users-roles/users/{Guid.NewGuid()}/role",
            new { newRole = "Admin" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_AdminCannotChangeOwnRole()
    {
        var userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.Setup(context => context.GetUserId()).Returns(userId);
        var adminService = new Mock<IAdminService>(MockBehavior.Strict);
        var controller = new AdminUsersRolesController(adminService.Object, currentUser.Object);

        var result = await controller.UpdateRole(
            userId,
            new UpdateUserRoleRequest { NewRole = "Parent" },
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.Forbidden, objectResult.StatusCode);
        adminService.VerifyNoOtherCalls();
    }

    private static string CreateToken(Guid userId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            ExportJobsWebApplicationFactory.JwtSecretForTests));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: "SugarGuardAPI",
            audience: "SugarGuardClients",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
