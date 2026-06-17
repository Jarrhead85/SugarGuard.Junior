using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SugarGuard.Application.Security;
using SugarGuard.Domain.Entities;
using SugarGuard.API.Application.Interfaces;

namespace SugarGuard.API.Application.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly IRolePermissionService _rolePermissionService;

    public JwtTokenService(IConfiguration configuration, IRolePermissionService rolePermissionService)
    {
        _configuration = configuration;
        _rolePermissionService = rolePermissionService;
    }

    public string GenerateToken(User user)
    {
        var secretKey = _configuration["Jwt:Secret"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
        var issuer = _configuration["Jwt:Issuer"] ?? "SugarGuardAPI";
        var audience = _configuration["Jwt:Audience"] ?? "SugarGuardClients";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
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

        var expiryHours = _configuration.GetValue<int>("Jwt:ExpiryHours", 24);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
