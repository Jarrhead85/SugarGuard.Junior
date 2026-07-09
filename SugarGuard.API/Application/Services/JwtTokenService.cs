using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SugarGuard.Application.Security;
using SugarGuard.Domain.Entities;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Security;

namespace SugarGuard.API.Application.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly IRolePermissionService _rolePermissionService;

    public JwtTokenService(JwtSettings settings, IRolePermissionService rolePermissionService)
    {
        _settings = settings;
        _rolePermissionService = rolePermissionService;
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim("UserId", user.UserId.ToString()),
        };
        if (user.TelegramId.HasValue)
            claims.Add(new Claim("TelegramId", user.TelegramId.Value.ToString()));

        claims.Add(new Claim(ClaimTypes.Role, user.Role.ToString()));
        claims.Add(new Claim("role", user.Role.ToString()));

        foreach (var permission in _rolePermissionService.GetPermissions(user.Role))
        {
            claims.Add(new Claim("permission", permission));
        }

        var token = new JwtSecurityToken(
            _settings.Issuer,
            _settings.Audience,
            claims,
            expires: DateTime.UtcNow.AddHours(_settings.ExpiryHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
