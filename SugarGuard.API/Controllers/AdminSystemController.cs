using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Системная статистика для панели администратора.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/system")]
public class AdminSystemController(
    AppDbContext context,
    IAdminService adminService,
    IServerMetricsService serverMetricsService,
    IConfiguration configuration,
    ILogger<AdminSystemController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает системную статистику для дашборда администратора.
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

    /// <summary>
    /// Возвращает метрики сервера: CPU, память, диск, сеть и uptime.
    /// </summary>
    [HttpGet("server-metrics")]
    public async Task<ActionResult<ServerMetricsResponse>> GetServerMetrics(CancellationToken ct)
    {
        try
        {
            var result = await serverMetricsService.GetSnapshotAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении метрик сервера");
            return StatusCode(500, new { message = "Не удалось получить метрики сервера" });
        }
    }

    /// <summary>
    /// Возвращает агрегированный расход токенов GigaChat без PHI.
    /// </summary>
    [HttpGet("gigachat-usage")]
    public async Task<ActionResult<GigaChatUsageResponse>> GetGigaChatUsage(CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var budget = configuration.GetValue<int?>("GigaChat:MonthlyTokenBudget");
            var monthUsage = await BuildUsagePeriodAsync(monthStart, ct);

            return Ok(new GigaChatUsageResponse
            {
                GeneratedAtUtc = now,
                Today = await BuildUsagePeriodAsync(today, ct),
                Month = monthUsage,
                AllTime = await BuildUsagePeriodAsync(null, ct),
                Children = await BuildChildUsageAsync(monthStart, ct),
                MonthlyTokenBudget = budget,
                MonthlyTokensRemaining = budget.HasValue
                    ? Math.Max(0, budget.Value - monthUsage.TotalTokens)
                    : null
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении расхода токенов GigaChat");
            return StatusCode(500, new { message = "Не удалось получить расход токенов" });
        }
    }

    /// <summary>
    /// Быстрая проверка доступности сервера и базы данных.
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        try
        {
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

    private async Task<GigaChatUsagePeriod> BuildUsagePeriodAsync(DateTime? fromUtc, CancellationToken ct)
    {
        var query = context.Set<AiConversationMessage>()
            .AsNoTracking()
            .Where(message => message.Role == AiMessageRole.Assistant)
            .Where(message => message.InputTokens.HasValue || message.OutputTokens.HasValue);

        if (fromUtc.HasValue)
        {
            query = query.Where(message => message.CreatedAt >= fromUtc.Value);
        }

        var rows = await query
            .Select(message => new
            {
                Input = message.InputTokens ?? 0,
                Output = message.OutputTokens ?? 0,
                Total = (message.InputTokens ?? 0) + (message.OutputTokens ?? 0)
            })
            .ToListAsync(ct);

        return new GigaChatUsagePeriod
        {
            ResponsesWithUsage = rows.Count,
            InputTokens = rows.Sum(row => row.Input),
            OutputTokens = rows.Sum(row => row.Output),
            TotalTokens = rows.Sum(row => row.Total)
        };
    }

    private async Task<IReadOnlyList<GigaChatChildUsage>> BuildChildUsageAsync(
        DateTime monthStartUtc,
        CancellationToken ct)
    {
        var rows = await context.Set<AiConversationMessage>()
            .AsNoTracking()
            .Where(message => message.Role == AiMessageRole.Assistant)
            .Where(message => message.InputTokens.HasValue || message.OutputTokens.HasValue)
            .Select(message => new ChildUsageRow(
                message.Conversation.ChildId,
                (message.Conversation.Child.FirstName + " " + message.Conversation.Child.LastName).Trim(),
                message.CreatedAt,
                message.InputTokens ?? 0,
                message.OutputTokens ?? 0))
            .ToListAsync(ct);

        return rows
            .GroupBy(row => new { row.ChildId, row.ChildDisplayName })
            .Select(group => new GigaChatChildUsage
            {
                ChildId = group.Key.ChildId,
                ChildDisplayName = string.IsNullOrWhiteSpace(group.Key.ChildDisplayName)
                    ? "Ребёнок"
                    : group.Key.ChildDisplayName,
                Month = BuildUsagePeriod(group.Where(row => row.CreatedAt >= monthStartUtc)),
                AllTime = BuildUsagePeriod(group)
            })
            .OrderByDescending(child => child.Month.TotalTokens)
            .ThenBy(child => child.ChildDisplayName)
            .ToArray();
    }

    private static GigaChatUsagePeriod BuildUsagePeriod(IEnumerable<ChildUsageRow> rows)
    {
        var items = rows.ToArray();
        return new GigaChatUsagePeriod
        {
            ResponsesWithUsage = items.Length,
            InputTokens = items.Sum(row => row.InputTokens),
            OutputTokens = items.Sum(row => row.OutputTokens),
            TotalTokens = items.Sum(row => row.InputTokens + row.OutputTokens)
        };
    }

    private sealed record ChildUsageRow(
        Guid ChildId,
        string ChildDisplayName,
        DateTime CreatedAt,
        int InputTokens,
        int OutputTokens);
}
