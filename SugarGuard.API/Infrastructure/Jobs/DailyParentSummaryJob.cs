using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Infrastructure.Jobs;

/// <summary>
/// Sends a single daily child summary to each parent at 21:00 Moscow time.
/// Delivery channels are independent: inbox is always persisted, while email and
/// Telegram are sent when the parent has the corresponding destination configured.
/// </summary>
public sealed class DailyParentSummaryJob
{
    private const string SourceType = "daily_parent_summary";
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ITelegramNotificationService _telegramService;
    private readonly IMaxBotService _maxService;
    private readonly ILogger<DailyParentSummaryJob> _logger;

    public DailyParentSummaryJob(
        AppDbContext db,
        IEmailService emailService,
        ITelegramNotificationService telegramService,
        IMaxBotService maxService,
        ILogger<DailyParentSummaryJob> logger)
    {
        _db = db;
        _emailService = emailService;
        _telegramService = telegramService;
        _maxService = maxService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 300 })]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MoscowTimeZone);
        var localDay = DateOnly.FromDateTime(now);
        var periodStart = ToUtc(localDay, TimeOnly.MinValue);
        var periodEnd = ToUtc(localDay.AddDays(1), TimeOnly.MinValue);

        var subscriptions = await _db.ParentChildLinks
            .AsNoTracking()
            .Join(_db.Users.AsNoTracking(), link => link.ParentUserId, user => user.UserId, (link, user) => new { link, user })
            .Join(_db.Children.AsNoTracking(), value => value.link.ChildId, child => child.ChildId, (value, child) => new DailySummaryRecipient(
                value.user.UserId,
                value.user.EmailForLogin,
                value.user.IsEmailVerified,
                value.user.TelegramId,
                value.user.MaxUserId,
                child.ChildId,
                child.FirstName,
                child.LastName))
            .ToListAsync(cancellationToken);

        var enabledParentIds = await _db.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.DailySummaryEnabled)
            .Select(user => user.UserId)
            .ToHashSetAsync(cancellationToken);

        var queued = 0;
        foreach (var recipient in subscriptions.Where(value => enabledParentIds.Contains(value.ParentUserId)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceId = CreateSourceId(recipient.ParentUserId, recipient.ChildId, localDay);
            var alreadyCreated = await _db.UserNotifications
                .AsNoTracking()
                .AnyAsync(notification => notification.RecipientUserId == recipient.ParentUserId
                                          && notification.SourceType == SourceType
                                          && notification.SourceId == sourceId,
                    cancellationToken);
            if (alreadyCreated)
            {
                continue;
            }

            var summary = await BuildSummaryAsync(recipient, periodStart, periodEnd, cancellationToken);
            _db.UserNotifications.Add(new UserNotification
            {
                RecipientUserId = recipient.ParentUserId,
                ChildId = recipient.ChildId,
                Type = "info",
                Title = $"Ежедневная сводка: {summary.ChildName}",
                Description = summary.InboxText,
                SourceType = SourceType,
                SourceId = sourceId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
            await _db.SaveChangesAsync(cancellationToken);
            queued++;

            await SendEmailAsync(recipient, summary, cancellationToken);
            await SendTelegramAsync(recipient, summary, cancellationToken);
            await SendMaxAsync(recipient, summary, cancellationToken);
        }

        _logger.LogInformation("Daily parent summaries completed. Date={Date} Persisted={Count}", localDay, queued);
    }

    public static void ScheduleRecurringJob() => RecurringJob.AddOrUpdate<DailyParentSummaryJob>(
        "daily-parent-summaries",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily(hour: 21, minute: 0),
        new RecurringJobOptions { TimeZone = MoscowTimeZone });

    private async Task<DailySummary> BuildSummaryAsync(
        DailySummaryRecipient recipient,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken)
    {
        var settings = await _db.DiabetesSettings
            .AsNoTracking()
            .Where(setting => setting.ChildId == recipient.ChildId)
            .Select(setting => new { setting.TargetRangeMin, setting.TargetRangeMax })
            .FirstOrDefaultAsync(cancellationToken);
        var targetMin = settings?.TargetRangeMin ?? 4.0m;
        var targetMax = settings?.TargetRangeMax ?? 10.0m;

        var measurements = await _db.Measurements
            .AsNoTracking()
            .Where(measurement => measurement.ChildId == recipient.ChildId
                                  && measurement.MeasurementTime >= periodStart
                                  && measurement.MeasurementTime < periodEnd)
            .Select(measurement => measurement.GlucoseValue)
            .ToListAsync(cancellationToken);

        var nutrition = await _db.NutritionEntries
            .AsNoTracking()
            .Where(entry => entry.ChildId == recipient.ChildId
                            && entry.RecordedAt >= periodStart
                            && entry.RecordedAt < periodEnd)
            .GroupBy(_ => 1)
            .Select(group => new { BreadUnits = group.Sum(item => item.BreadUnits), InsulinUnits = group.Sum(item => item.InsulinUnits) })
            .FirstOrDefaultAsync(cancellationToken);

        var childName = string.Join(' ', new[] { recipient.FirstName, recipient.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        childName = string.IsNullOrWhiteSpace(childName) ? "Ребёнок" : childName;
        var glucoseLine = measurements.Count == 0
            ? "Измерений глюкозы сегодня не было."
            : $"Глюкоза: {measurements.Count} изм., средняя {measurements.Average():0.0} ммоль/л, диапазон {measurements.Min():0.0}–{measurements.Max():0.0} ммоль/л, в цели {measurements.Count(value => value >= targetMin && value <= targetMax) * 100 / measurements.Count}%.";
        var nutritionLine = $"Питание: {nutrition?.BreadUnits ?? 0m:0.##} ХЕ, инсулин {nutrition?.InsulinUnits ?? 0m:0.##} ед.";
        return new DailySummary(
            childName,
            glucoseLine,
            nutritionLine,
            $"{glucoseLine} {nutritionLine}",
            $"<p>Сводка за сегодня по ребёнку <strong>{WebUtility.HtmlEncode(childName)}</strong>.</p><p>{WebUtility.HtmlEncode(glucoseLine)}</p><p>{WebUtility.HtmlEncode(nutritionLine)}</p><p>Полная история доступна в кабинете SugarGuard.</p>");
    }

    private async Task SendEmailAsync(DailySummaryRecipient recipient, DailySummary summary, CancellationToken cancellationToken)
    {
        if (!recipient.IsEmailVerified || string.IsNullOrWhiteSpace(recipient.Email))
        {
            return;
        }

        try
        {
            await _emailService.SendAsync(recipient.Email, $"SugarGuard: сводка за день — {summary.ChildName}", summary.EmailHtml, summary.InboxText, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Daily summary email delivery failed. ParentUserId={ParentUserId}", recipient.ParentUserId);
        }
    }

    private async Task SendTelegramAsync(DailySummaryRecipient recipient, DailySummary summary, CancellationToken cancellationToken)
    {
        if (!recipient.TelegramId.HasValue)
        {
            return;
        }

        try
        {
            await _telegramService.SendDailySummaryAsync(recipient.TelegramId.Value, $"📋 Сводка SugarGuard\nРебёнок: {summary.ChildName}\n{summary.InboxText}", cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Daily summary Telegram delivery failed. ParentUserId={ParentUserId}", recipient.ParentUserId);
        }
    }

    private async Task SendMaxAsync(DailySummaryRecipient recipient, DailySummary summary, CancellationToken cancellationToken)
    {
        if (!recipient.MaxUserId.HasValue || !_maxService.IsConfigured)
        {
            return;
        }

        try
        {
            await _maxService.SendDailySummaryAsync(recipient.MaxUserId.Value, $"📋 Сводка SugarGuard\nРебёнок: {summary.ChildName}\n{summary.InboxText}", cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Daily summary MAX delivery failed. ParentUserId={ParentUserId}", recipient.ParentUserId);
        }
    }

    private static Guid CreateSourceId(Guid parentUserId, Guid childId, DateOnly date)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{SourceType}:{parentUserId:N}:{childId:N}:{date:yyyy-MM-dd}"));
        return new Guid(bytes[..16]);
    }

    private static DateTime ToUtc(DateOnly date, TimeOnly time) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), MoscowTimeZone);

    private static readonly TimeZoneInfo MoscowTimeZone = ResolveMoscowTimeZone();

    private static TimeZoneInfo ResolveMoscowTimeZone()
    {
        foreach (var id in new[] { "Europe/Moscow", "Russian Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Utc;
    }

    private sealed record DailySummaryRecipient(Guid ParentUserId, string? Email, bool IsEmailVerified, long? TelegramId, long? MaxUserId, Guid ChildId, string? FirstName, string? LastName);
    private sealed record DailySummary(string ChildName, string GlucoseLine, string NutritionLine, string InboxText, string EmailHtml);
}
