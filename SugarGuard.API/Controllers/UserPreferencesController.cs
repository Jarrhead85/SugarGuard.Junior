using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;

namespace SugarGuard.API.Controllers;

[Authorize]
[ApiController]
[Route("api/user-preferences")]
public class UserPreferencesController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;
    private readonly ILogger<UserPreferencesController> _logger;

    private const string CachePrefix = "userpref:";

    public UserPreferencesController(
        IMemoryCache cache,
        AppDbContext db,
        ILogger<UserPreferencesController> logger)
    {
        _cache = cache;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<UserPreferencesDto>> GetPreferences(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("UserId")
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var cacheKey = $"{CachePrefix}{userId}";
        var persistedPreferences = Guid.TryParse(userId, out var parsedUserId)
            ? await _db.Users
                .AsNoTracking()
                .Where(user => user.UserId == parsedUserId)
                .Select(user => new { user.MapProvider, user.DailySummaryEnabled })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        if (_cache.TryGetValue(cacheKey, out UserPreferencesDto? prefs) && prefs is not null)
        {
            return Ok(new UserPreferencesDto
            {
                AlertsCritical = prefs.AlertsCritical,
                AlertsDailySummary = persistedPreferences?.DailySummaryEnabled ?? prefs.AlertsDailySummary,
                AlertsMissedMeasurement = prefs.AlertsMissedMeasurement,
                MapProvider = NormalizeMapProvider(persistedPreferences?.MapProvider)
            });
        }

        return Ok(new UserPreferencesDto
        {
            AlertsDailySummary = persistedPreferences?.DailySummaryEnabled ?? true,
            MapProvider = NormalizeMapProvider(persistedPreferences?.MapProvider)
        });
    }

    [HttpPut]
    public async Task<ActionResult> SavePreferences(
        [FromBody] SaveUserPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("UserId")
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var cacheKey = $"{CachePrefix}{userId}";
        var normalizedMapProvider = NormalizeMapProvider(request.MapProvider);
        if (Guid.TryParse(userId, out var parsedUserId))
        {
            var user = await _db.Users.FirstOrDefaultAsync(candidate => candidate.UserId == parsedUserId, cancellationToken);
            if (user is not null && (user.MapProvider != normalizedMapProvider
                                     || user.DailySummaryEnabled != request.AlertsDailySummary))
            {
                user.MapProvider = normalizedMapProvider;
                user.DailySummaryEnabled = request.AlertsDailySummary;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        _cache.Set(cacheKey, new UserPreferencesDto
        {
            AlertsCritical = request.AlertsCritical,
            AlertsDailySummary = request.AlertsDailySummary,
            AlertsMissedMeasurement = request.AlertsMissedMeasurement,
            MapProvider = normalizedMapProvider
        }, TimeSpan.FromDays(7));

        _logger.LogInformation("Сохранены предпочтения уведомлений для пользователя {UserId}", userId);
        return Ok();
    }

    private static string NormalizeMapProvider(string? provider) => provider?.Trim().ToLowerInvariant() switch
    {
        "yandex" or "google" or "openstreetmap" => provider.Trim().ToLowerInvariant(),
        _ => "yandex"
    };
}
