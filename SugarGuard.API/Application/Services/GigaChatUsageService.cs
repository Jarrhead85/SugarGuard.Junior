using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Изолирует чтение данных о токенах GigaChat от HTTP-контроллера.
/// </summary>
public sealed class GigaChatUsageService : IGigaChatUsageService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;

    public GigaChatUsageService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    public async Task<GigaChatUsageResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var today = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var budget = _configuration.GetValue<int?>("GigaChat:MonthlyTokenBudget");
        var monthUsage = await BuildUsagePeriodAsync(db, monthStart, cancellationToken);

        return new GigaChatUsageResponse
        {
            GeneratedAtUtc = now,
            Today = await BuildUsagePeriodAsync(db, today, cancellationToken),
            Month = monthUsage,
            AllTime = await BuildUsagePeriodAsync(db, null, cancellationToken),
            Children = await BuildChildUsageAsync(db, monthStart, cancellationToken),
            MonthlyTokenBudget = budget,
            MonthlyTokensRemaining = budget.HasValue
                ? Math.Max(0, budget.Value - monthUsage.TotalTokens)
                : null
        };
    }

    private static async Task<GigaChatUsagePeriod> BuildUsagePeriodAsync(
        AppDbContext db,
        DateTime? fromUtc,
        CancellationToken cancellationToken)
    {
        var query = db.Set<AiConversationMessage>()
            .AsNoTracking()
            .Where(message => message.Role == AiMessageRole.Assistant)
            .Where(message => message.InputTokens.HasValue || message.OutputTokens.HasValue);

        if (fromUtc.HasValue)
        {
            query = query.Where(message => message.CreatedAt >= fromUtc.Value);
        }

        var rows = await query
            .Select(message => new TokenUsageRow(
                message.InputTokens ?? 0,
                message.OutputTokens ?? 0))
            .ToListAsync(cancellationToken);

        return BuildUsagePeriod(rows);
    }

    private static async Task<IReadOnlyList<GigaChatChildUsage>> BuildChildUsageAsync(
        AppDbContext db,
        DateTime monthStartUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.Set<AiConversationMessage>()
            .AsNoTracking()
            .Where(message => message.Role == AiMessageRole.Assistant)
            .Where(message => message.InputTokens.HasValue || message.OutputTokens.HasValue)
            .Select(message => new ChildUsageRow(
                message.Conversation.ChildId,
                (message.Conversation.Child.FirstName + " " + message.Conversation.Child.LastName).Trim(),
                message.CreatedAt,
                message.InputTokens ?? 0,
                message.OutputTokens ?? 0))
            .ToListAsync(cancellationToken);

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

    private static GigaChatUsagePeriod BuildUsagePeriod(IEnumerable<TokenUsageRow> rows)
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

    private static GigaChatUsagePeriod BuildUsagePeriod(IEnumerable<ChildUsageRow> rows)
        => BuildUsagePeriod(rows.Select(row => new TokenUsageRow(row.InputTokens, row.OutputTokens)));

    private sealed record TokenUsageRow(int InputTokens, int OutputTokens);
    private sealed record ChildUsageRow(
        Guid ChildId,
        string ChildDisplayName,
        DateTime CreatedAt,
        int InputTokens,
        int OutputTokens);
}
