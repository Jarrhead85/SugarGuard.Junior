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
            PromptVersions = await BuildPromptVersionUsageAsync(db, monthStart, cancellationToken),
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
                message.OutputTokens ?? 0,
                message.PrecachedPromptTokens ?? 0,
                message.SafetyResult == AiSafetyResult.BlockedUnsafeOutput))
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
                message.OutputTokens ?? 0,
                message.PrecachedPromptTokens ?? 0,
                message.SafetyResult == AiSafetyResult.BlockedUnsafeOutput))
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

    private static async Task<IReadOnlyList<GigaChatPromptVersionUsage>> BuildPromptVersionUsageAsync(
        AppDbContext db,
        DateTime monthStartUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.Set<AiConversationMessage>()
            .AsNoTracking()
            .Where(message => message.Role == AiMessageRole.Assistant)
            .Where(message => message.InputTokens.HasValue || message.OutputTokens.HasValue)
            .Select(message => new PromptVersionUsageRow(
                message.PromptVersion,
                message.CreatedAt,
                message.InputTokens ?? 0,
                message.OutputTokens ?? 0,
                message.PrecachedPromptTokens ?? 0,
                message.SafetyResult == AiSafetyResult.BlockedUnsafeOutput))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => string.IsNullOrWhiteSpace(row.PromptVersion)
                ? "historical-unversioned"
                : row.PromptVersion)
            .Select(group => new GigaChatPromptVersionUsage
            {
                PromptVersion = group.Key,
                Month = BuildUsagePeriod(group.Where(row => row.CreatedAt >= monthStartUtc)),
                AllTime = BuildUsagePeriod(group)
            })
            .OrderByDescending(version => version.Month.TotalTokens)
            .ThenBy(version => version.PromptVersion)
            .ToArray();
    }

    private static GigaChatUsagePeriod BuildUsagePeriod(IEnumerable<TokenUsageRow> rows)
    {
        var items = rows.ToArray();
        var inputTokens = items.Sum(row => row.InputTokens);
        var outputTokens = items.Sum(row => row.OutputTokens);
        var precachedPromptTokens = items.Sum(row => row.PrecachedPromptTokens);

        return new GigaChatUsagePeriod
        {
            ResponsesWithUsage = items.Length,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            PrecachedPromptTokens = precachedPromptTokens,
            SafetyPolicyReplacements = items.Count(row => row.WasSafetyPolicyReplacement),
            TotalTokens = inputTokens + outputTokens
        };
    }

    private static GigaChatUsagePeriod BuildUsagePeriod(IEnumerable<ChildUsageRow> rows)
        => BuildUsagePeriod(rows.Select(row => new TokenUsageRow(
            row.InputTokens,
            row.OutputTokens,
            row.PrecachedPromptTokens,
            row.WasSafetyPolicyReplacement)));

    private static GigaChatUsagePeriod BuildUsagePeriod(IEnumerable<PromptVersionUsageRow> rows)
        => BuildUsagePeriod(rows.Select(row => new TokenUsageRow(
            row.InputTokens,
            row.OutputTokens,
            row.PrecachedPromptTokens,
            row.WasSafetyPolicyReplacement)));

    private sealed record TokenUsageRow(
        int InputTokens,
        int OutputTokens,
        int PrecachedPromptTokens,
        bool WasSafetyPolicyReplacement);
    private sealed record ChildUsageRow(
        Guid ChildId,
        string ChildDisplayName,
        DateTime CreatedAt,
        int InputTokens,
        int OutputTokens,
        int PrecachedPromptTokens,
        bool WasSafetyPolicyReplacement);
    private sealed record PromptVersionUsageRow(
        string? PromptVersion,
        DateTime CreatedAt,
        int InputTokens,
        int OutputTokens,
        int PrecachedPromptTokens,
        bool WasSafetyPolicyReplacement);
}
