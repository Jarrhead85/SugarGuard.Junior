using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Health-check эндпоинт
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _health;

    public HealthController(IHealthService health)
    {
        _health = health;
    }

    /// <summary>
    /// Проверяет доступность БД
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var canConnect = await _health.CanConnectAsync(cancellationToken);
        return Ok(new
        {
            status = canConnect ? "ok" : "degraded",
            timestampUtc = DateTime.UtcNow
        });
    }
}
