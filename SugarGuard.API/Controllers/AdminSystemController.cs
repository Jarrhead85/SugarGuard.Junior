using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Системная статистика для панели администратора
/// </summary>
[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/system")]
public class AdminSystemController(
    AppDbContext context,
    IAdminService adminService,
    ILogger<AdminSystemController> logger) : ControllerBase
{
    // GET api/admin/system/stats
    /// <summary>
    /// Возвращает системную статистику для дашборда администратора
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        try
        {
            var result = await adminService.GetSystemStatsAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении системной статистики");
            return StatusCode(500, new { message = "Не удалось получить статистику" });
        }
    }

    // GET api/admin/system/health
    /// <summary>
    /// Быстрая проверка доступности сервера и базы данных
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        try
        {
            // Проверяем соединение с БД
            var canConnect = await context.Database.CanConnectAsync(ct);

            return Ok(new
            {
                status = canConnect ? "healthy" : "degraded",
                database = canConnect ? "ok" : "unavailable",
                serverUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check завершился с ошибкой");
            return StatusCode(503, new
            {
                status = "unhealthy",
                database = "error",
                serverUtc = DateTime.UtcNow
            });
        }
    }
}
