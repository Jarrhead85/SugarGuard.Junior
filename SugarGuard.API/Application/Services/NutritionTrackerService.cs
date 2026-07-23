using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Services;

public sealed class NutritionTrackerService : INutritionTrackerService
{
    // Excel для Windows надёжно определяет UTF-16 LE по BOM при двойном клике по CSV.
    // Это не зависит от системной ANSI-кодировки и настроек импорта пользователя.
    private static readonly UnicodeEncoding CsvEncoding = new(bigEndian: false, byteOrderMark: true);

    private static readonly AchievementDefinition[] AchievementDefinitions =
    [
        new("first_steps", "Первые шаги", "Заполни первые 3 записи дневника", "achievement_first_steps.png", 3),
        new("food_week", "Неделя порядка", "Заполняй питание 7 разных дней", "achievement_food_week.png", 7),
        new("insulin_20", "Ответственный герой", "Отметь 20 введений инсулина", "achievement_insulin_20.png", 20),
        new("target_10", "Десять точных дней", "10 дней с измерениями в целевом диапазоне", "achievement_target_10.png", 10),
        new("schedule_7", "По расписанию", "7 дней отмечай приёмы пищи", "achievement_schedule_7.png", 7),
        new("backpack_10", "Запасливый путешественник", "Отметь 10 съеденных перекусов", "achievement_backpack_10.png", 10)
    ];

    private readonly AppDbContext _context;
    private readonly ICurrentUserContext _currentUser;

    public NutritionTrackerService(AppDbContext context, ICurrentUserContext currentUser)
    {
        _context = context;
        _currentUser = currentUser;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<IReadOnlyList<NutritionEntryResponse>> GetEntriesAsync(
        Guid childId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken) =>
        await _context.NutritionEntries
            .AsNoTracking()
            .Where(entry => entry.ChildId == childId && entry.RecordedAt >= from && entry.RecordedAt <= to)
            .OrderByDescending(entry => entry.RecordedAt)
            .Select(entry => MapEntry(entry))
            .ToListAsync(cancellationToken);

    public async Task<NutritionEntryResponse> CreateEntryAsync(
        Guid childId,
        Guid actorId,
        SaveNutritionEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entity = new NutritionEntry { ChildId = childId, CreatedByUserId = actorId };
        Apply(entity, request);
        entity.Source = ResolveSource();
        _context.NutritionEntries.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await RefreshAchievementsAsync(childId, cancellationToken);
        return MapEntry(entity);
    }

    public async Task<NutritionEntryResponse?> UpdateEntryAsync(
        Guid childId,
        Guid entryId,
        SaveNutritionEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _context.NutritionEntries
            .FirstOrDefaultAsync(entry => entry.ChildId == childId && entry.NutritionEntryId == entryId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        Apply(entity, request);
        await _context.SaveChangesAsync(cancellationToken);
        return MapEntry(entity);
    }

    public async Task<bool> DeleteEntryAsync(Guid childId, Guid entryId, CancellationToken cancellationToken)
    {
        var deleted = await _context.NutritionEntries
            .Where(entry => entry.ChildId == childId && entry.NutritionEntryId == entryId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<IReadOnlyList<MealScheduleResponse>> GetSchedulesAsync(Guid childId, CancellationToken cancellationToken) =>
        await _context.MealSchedules
            .AsNoTracking()
            .Where(schedule => schedule.ChildId == childId)
            .OrderBy(schedule => schedule.ScheduledTime)
            .Select(schedule => MapSchedule(schedule))
            .ToListAsync(cancellationToken);

    public async Task<MealScheduleResponse> CreateScheduleAsync(
        Guid childId,
        SaveMealScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        var existing = await _context.MealSchedules.FirstOrDefaultAsync(
            schedule => schedule.ChildId == childId
                        && schedule.ScheduledTime == request.ScheduledTime
                        && schedule.Title == title,
            cancellationToken);

        if (existing is not null)
        {
            Apply(existing, request);
            await _context.SaveChangesAsync(cancellationToken);
            return MapSchedule(existing);
        }

        var entity = new MealSchedule { ChildId = childId };
        Apply(entity, request);
        _context.MealSchedules.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return MapSchedule(entity);
    }

    public async Task<MealScheduleResponse?> UpdateScheduleAsync(
        Guid childId,
        Guid scheduleId,
        SaveMealScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _context.MealSchedules
            .FirstOrDefaultAsync(schedule => schedule.ChildId == childId && schedule.MealScheduleId == scheduleId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        Apply(entity, request);
        await _context.SaveChangesAsync(cancellationToken);
        return MapSchedule(entity);
    }

    public async Task<bool> DeleteScheduleAsync(Guid childId, Guid scheduleId, CancellationToken cancellationToken) =>
        await _context.MealSchedules
            .Where(schedule => schedule.ChildId == childId && schedule.MealScheduleId == scheduleId)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<NutritionSummaryResponse> GetSummaryAsync(
        Guid childId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var rows = await _context.NutritionEntries
            .AsNoTracking()
            .Where(entry => entry.ChildId == childId && entry.RecordedAt >= from && entry.RecordedAt <= to)
            .Select(entry => new { entry.RecordedAt, entry.BreadUnits, entry.InsulinUnits })
            .ToListAsync(cancellationToken);

        var days = rows
            .GroupBy(row => DateOnly.FromDateTime(ToDisplayTime(row.RecordedAt)))
            .OrderBy(group => group.Key)
            .Select(group => new NutritionDailySummary(
                group.Key,
                group.Sum(row => row.BreadUnits),
                group.Sum(row => row.InsulinUnits),
                group.Count()))
            .ToList();

        var dayByDate = days.ToDictionary(day => day.Date);
        var fullPeriodDays = EnumerateDates(
                DateOnly.FromDateTime(ToDisplayTime(from)),
                DateOnly.FromDateTime(ToDisplayTime(to)))
            .Select(date => dayByDate.TryGetValue(date, out var day)
                ? day
                : new NutritionDailySummary(date, 0m, 0m, 0))
            .ToList();

        var daysWithEntries = Math.Max(1, days.Count);
        var totalXe = rows.Sum(row => row.BreadUnits);
        var totalInsulin = rows.Sum(row => row.InsulinUnits);

        return new NutritionSummaryResponse(
            from,
            to,
            totalXe,
            totalInsulin,
            Math.Round(totalXe / daysWithEntries, 2),
            Math.Round(totalInsulin / daysWithEntries, 2),
            fullPeriodDays);
    }

    public async Task<IReadOnlyList<AchievementResponse>> GetAchievementsAsync(Guid childId, CancellationToken cancellationToken)
    {
        await RefreshAchievementsAsync(childId, cancellationToken);
        var progress = await GetAchievementProgressAsync(childId, cancellationToken);
        var unlocked = await _context.ChildAchievements
            .AsNoTracking()
            .Where(item => item.ChildId == childId)
            .ToDictionaryAsync(item => item.AchievementCode, item => item.UnlockedAt, cancellationToken);

        return AchievementDefinitions
            .Select(definition => new AchievementResponse(
                definition.Code,
                definition.Title,
                definition.Description,
                definition.ImageName,
                Math.Min(progress[definition.Code], definition.Target),
                definition.Target,
                unlocked.ContainsKey(definition.Code),
                unlocked.GetValueOrDefault(definition.Code)))
            .ToList();
    }

    public async Task<byte[]> ExportCsvAsync(Guid childId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var entries = await GetEntriesAsync(childId, from, to, cancellationToken);
        var builder = new StringBuilder("sep=;\r\n");
        builder.AppendLine(ToCsvRow("Дата и время", "Приём пищи", "Описание", "ХЕ", "Инсулин, ед.", "Глюкоза до", "Источник", "Заметка"));

        foreach (var entry in entries.OrderBy(entry => entry.RecordedAt))
        {
            builder.AppendLine(ToCsvRow(
                ToDisplayTime(entry.RecordedAt).ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU")),
                MealLabel(entry.MealType),
                entry.MealName,
                entry.BreadUnits.ToString("0.##", CultureInfo.InvariantCulture),
                entry.InsulinUnits.ToString("0.##", CultureInfo.InvariantCulture),
                entry.GlucoseBefore?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty,
                entry.Source.ToString(),
                entry.Notes));
        }

        // Unicode UTF-16 LE с BOM корректно определяется Microsoft Excel на Windows
        // при открытии файла из браузера без ручного выбора кодировки.
        return CsvEncoding.GetPreamble().Concat(CsvEncoding.GetBytes(builder.ToString())).ToArray();
    }

    public async Task<byte[]> ExportPdfAsync(Guid childId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var entries = await GetEntriesAsync(childId, from, to, cancellationToken);
        var childName = await _context.Children
            .AsNoTracking()
            .Where(child => child.ChildId == childId)
            .Select(child => child.FirstName + " " + child.LastName)
            .SingleAsync(cancellationToken);

        var locale = CultureInfo.GetCultureInfo("ru-RU");
        var entriesByDay = entries
            .OrderBy(item => item.RecordedAt)
            .GroupBy(item => ToDisplayTime(item.RecordedAt).Date)
            .ToList();

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().Column(header =>
            {
                header.Item().Text("SugarGuard | Дневник питания")
                    .Bold()
                    .FontSize(18)
                    .FontColor(Colors.Teal.Darken2);
                header.Item().PaddingTop(3).Text($"{childName} · период: {ToDisplayTime(from):dd.MM.yyyy} - {ToDisplayTime(to):dd.MM.yyyy}")
                    .FontSize(10)
                    .FontColor(Colors.Grey.Darken1);
                header.Item().PaddingTop(10).Row(row =>
                {
                    row.AutoItem().Background(Colors.Teal.Lighten5).PaddingHorizontal(7).PaddingVertical(3)
                        .Text("Основные приёмы пищи").FontSize(8);
                    row.ConstantItem(8);
                    row.AutoItem().Background(Colors.Grey.Lighten4).PaddingHorizontal(7).PaddingVertical(3)
                        .Text("Перекусы").FontSize(8);
                });
            });
            page.Content().PaddingTop(16).Column(column =>
            {
                column.Spacing(10);

                foreach (var day in entriesByDay)
                {
                    column.Item().EnsureSpace(100).Column(dayColumn =>
                    {
                        dayColumn.Item()
                            .BorderBottom(1)
                            .BorderColor(Colors.Teal.Lighten2)
                            .PaddingBottom(4)
                            .Text(day.Key.ToString("dddd, d MMMM yyyy", locale))
                            .Bold()
                            .FontSize(12)
                            .FontColor(Colors.Teal.Darken2);

                        dayColumn.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(4);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            foreach (var header in new[] { "Время", "Тип", "Описание", "ХЕ", "Инсулин" })
                            {
                                table.Cell().Background(Colors.Teal.Lighten4).Padding(5).Text(header).Bold().FontSize(8);
                            }

                            foreach (var entry in day)
                            {
                                var rowColor = entry.MealType switch
                                {
                                    MealType.Snack => Colors.Grey.Lighten4,
                                    MealType.Other => Colors.Amber.Lighten5,
                                    _ => Colors.Teal.Lighten5
                                };

                                table.Cell().Background(rowColor).Padding(5).Text(ToDisplayTime(entry.RecordedAt).ToString("HH:mm"));
                                table.Cell().Background(rowColor).Padding(5).Text(MealLabel(entry.MealType));
                                table.Cell().Background(rowColor).Padding(5).Text(entry.MealName).FontSize(9);
                                table.Cell().Background(rowColor).Padding(5).Text(entry.BreadUnits.ToString("0.##", CultureInfo.InvariantCulture));
                                table.Cell().Background(rowColor).Padding(5).Text(entry.InsulinUnits.ToString("0.##", CultureInfo.InvariantCulture));
                            }
                        });
                    });
                }
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Сформировано SugarGuard | ");
                text.CurrentPageNumber();
            });
        })).GeneratePdf();
    }

    private async Task RefreshAchievementsAsync(Guid childId, CancellationToken cancellationToken)
    {
        var progress = await GetAchievementProgressAsync(childId, cancellationToken);
        var existing = await _context.ChildAchievements
            .Where(item => item.ChildId == childId)
            .Select(item => item.AchievementCode)
            .ToListAsync(cancellationToken);

        var additions = AchievementDefinitions
            .Where(definition => progress[definition.Code] >= definition.Target && !existing.Contains(definition.Code))
            .Select(definition => new ChildAchievement { ChildId = childId, AchievementCode = definition.Code });

        _context.ChildAchievements.AddRange(additions);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, int>> GetAchievementProgressAsync(Guid childId, CancellationToken cancellationToken)
    {
        var nutritionCount = await _context.NutritionEntries
            .CountAsync(entry => entry.ChildId == childId, cancellationToken);
        var nutritionDays = await _context.NutritionEntries
            .Where(entry => entry.ChildId == childId)
            .Select(entry => entry.RecordedAt.Date)
            .Distinct()
            .CountAsync(cancellationToken);
        var insulinCount = await _context.NutritionEntries
            .CountAsync(entry => entry.ChildId == childId && entry.InsulinUnits > 0, cancellationToken);
        var targetDays = await _context.Measurements
            .Where(measurement => measurement.ChildId == childId && measurement.GlucoseValue >= 4m && measurement.GlucoseValue <= 10m)
            .Select(measurement => measurement.MeasurementTime.Date)
            .Distinct()
            .CountAsync(cancellationToken);
        var snacks = await _context.SnackConsumptionLogs
            .CountAsync(log => log.ChildId == childId, cancellationToken);

        return new Dictionary<string, int>
        {
            ["first_steps"] = nutritionCount,
            ["food_week"] = nutritionDays,
            ["insulin_20"] = insulinCount,
            ["target_10"] = targetDays,
            ["schedule_7"] = nutritionDays,
            ["backpack_10"] = snacks
        };
    }

    private NutritionEntrySource ResolveSource() => _currentUser.GetRole() switch
    {
        UserRole.ChildDevice => NutritionEntrySource.Child,
        UserRole.Parent => NutritionEntrySource.Parent,
        UserRole.Doctor => NutritionEntrySource.Doctor,
        _ => NutritionEntrySource.Admin
    };

    private static void Apply(NutritionEntry entity, SaveNutritionEntryRequest request)
    {
        entity.RecordedAt = NormalizeToUtc(request.RecordedAt);
        entity.MealType = request.MealType;
        entity.MealName = request.MealName.Trim();
        entity.BreadUnits = request.BreadUnits;
        entity.InsulinUnits = request.InsulinUnits;
        entity.GlucoseBefore = request.GlucoseBefore;
        entity.Notes = request.Notes?.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
    }

    private static void Apply(MealSchedule entity, SaveMealScheduleRequest request)
    {
        entity.MealType = request.MealType;
        entity.Title = request.Title.Trim();
        entity.ScheduledTime = request.ScheduledTime;
        entity.PlannedBreadUnits = request.PlannedBreadUnits;
        entity.DaysOfWeekMask = request.DaysOfWeekMask;
        entity.ReminderEnabled = request.ReminderEnabled;
        entity.ReminderMinutesBefore = request.ReminderMinutesBefore;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    private static NutritionEntryResponse MapEntry(NutritionEntry entry) => new(
        entry.NutritionEntryId,
        entry.ChildId,
        entry.RecordedAt,
        entry.MealType,
        entry.MealName,
        entry.BreadUnits,
        entry.InsulinUnits,
        entry.GlucoseBefore,
        entry.Notes,
        entry.Source,
        entry.UpdatedAt);

    private static MealScheduleResponse MapSchedule(MealSchedule schedule) => new(
        schedule.MealScheduleId,
        schedule.ChildId,
        schedule.MealType,
        schedule.Title,
        schedule.ScheduledTime,
        schedule.PlannedBreadUnits,
        schedule.DaysOfWeekMask,
        schedule.ReminderEnabled,
        schedule.ReminderMinutesBefore,
        schedule.IsActive,
        schedule.UpdatedAt);

    private static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => TimeZoneInfo.ConvertTimeToUtc(value, DefaultTimeZone)
    };

    private static DateTime ToDisplayTime(DateTime utcTime) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            utcTime.Kind == DateTimeKind.Utc ? utcTime : DateTime.SpecifyKind(utcTime, DateTimeKind.Utc),
            DefaultTimeZone);

    private static IEnumerable<DateOnly> EnumerateDates(DateOnly from, DateOnly to)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static TimeZoneInfo DefaultTimeZone
    {
        get
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
            }
        }
    }

    private static string ToCsvRow(params string?[] values) => string.Join(';', values.Select(Csv));

    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static string MealLabel(MealType type) => type switch
    {
        MealType.Breakfast => "Завтрак",
        MealType.Lunch => "Обед",
        MealType.Dinner => "Ужин",
        MealType.Snack => "Перекус",
        _ => "Другое"
    };

    private sealed record AchievementDefinition(string Code, string Title, string Description, string ImageName, int Target);
}
