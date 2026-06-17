using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SugarGuard.API.DTOs;

namespace SugarGuard.API.Controllers;

[Authorize]
[ApiController]
[Route("api/user-preferences")]
public class UserPreferencesController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserPreferencesController> _logger;

    private const string CachePrefix = "userpref:";

    public UserPreferencesController(
        IMemoryCache cache,
        ILogger<UserPreferencesController> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<UserPreferencesDto> GetPreferences()
    {
        var userId = User.FindFirstValue("UserId")
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var cacheKey = $"{CachePrefix}{userId}";
        if (_cache.TryGetValue(cacheKey, out UserPreferencesDto? prefs) && prefs is not null)
            return Ok(prefs);

        return Ok(new UserPreferencesDto());
    }

    [HttpPut]
    public ActionResult SavePreferences([FromBody] SaveUserPreferencesRequest request)
    {
        var userId = User.FindFirstValue("UserId")
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var cacheKey = $"{CachePrefix}{userId}";
        _cache.Set(cacheKey, new UserPreferencesDto
        {
            AlertsCritical = request.AlertsCritical,
            AlertsDailySummary = request.AlertsDailySummary,
            AlertsMissedMeasurement = request.AlertsMissedMeasurement
        }, TimeSpan.FromDays(7));

        _logger.LogInformation("Сохранены предпочтения уведомлений для пользователя {UserId}", userId);
        return Ok();
    }
}
