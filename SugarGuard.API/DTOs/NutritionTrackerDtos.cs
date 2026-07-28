using System.ComponentModel.DataAnnotations;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.DTOs;

public sealed record NutritionEntryResponse(
    Guid NutritionEntryId,
    Guid ChildId,
    DateTime RecordedAt,
    MealType MealType,
    string MealName,
    decimal BreadUnits,
    decimal InsulinUnits,
    decimal? GlucoseBefore,
    string? Notes,
    NutritionEntrySource Source,
    DateTime UpdatedAt);

public sealed class SaveNutritionEntryRequest
{
    [Required]
    public DateTime RecordedAt { get; init; }

    [EnumDataType(typeof(MealType))]
    public MealType MealType { get; init; }

    [Required, StringLength(120, MinimumLength = 2)]
    public string MealName { get; init; } = string.Empty;

    [Range(0, 50)]
    public decimal BreadUnits { get; init; }

    [Range(0, 100)]
    public decimal InsulinUnits { get; init; }

    [Range(1, 33)]
    public decimal? GlucoseBefore { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}

public sealed record MealScheduleResponse(
    Guid MealScheduleId,
    Guid ChildId,
    MealType MealType,
    string Title,
    TimeOnly ScheduledTime,
    decimal? PlannedBreadUnits,
    int DaysOfWeekMask,
    bool ReminderEnabled,
    int ReminderMinutesBefore,
    bool IsActive,
    bool IsNightInsulin,
    int RepeatIntervalMinutes,
    int EscalationWindowMinutes,
    DateTime UpdatedAt);

public sealed class SaveMealScheduleRequest
{
    [EnumDataType(typeof(MealType))]
    public MealType MealType { get; init; }

    [Required, StringLength(80, MinimumLength = 2)]
    public string Title { get; init; } = string.Empty;

    public TimeOnly ScheduledTime { get; init; }

    [Range(0, 50)]
    public decimal? PlannedBreadUnits { get; init; }

    [Range(1, 127)]
    public int DaysOfWeekMask { get; init; } = 127;

    public bool ReminderEnabled { get; init; } = true;

    [Range(0, 180)]
    public int ReminderMinutesBefore { get; init; } = 10;

    public bool IsActive { get; init; } = true;

    public bool IsNightInsulin { get; init; }

    [Range(1, 30)]
    public int RepeatIntervalMinutes { get; init; } = 5;

    [Range(5, 120)]
    public int EscalationWindowMinutes { get; init; } = 60;
}

public sealed record NutritionDailySummary(
    DateOnly Date,
    decimal BreadUnits,
    decimal InsulinUnits,
    int EntriesCount);

public sealed record NutritionSummaryResponse(
    DateTime From,
    DateTime To,
    decimal TotalBreadUnits,
    decimal TotalInsulinUnits,
    decimal AverageBreadUnitsPerDay,
    decimal AverageInsulinUnitsPerDay,
    IReadOnlyList<NutritionDailySummary> Days);

public sealed record AchievementResponse(
    string Code,
    string Title,
    string Description,
    string ImageName,
    int Progress,
    int Target,
    bool IsUnlocked,
    DateTime? UnlockedAt);
