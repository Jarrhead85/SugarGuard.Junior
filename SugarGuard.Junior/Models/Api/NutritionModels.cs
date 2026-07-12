using SugarGuard.Domain.Enums;

namespace SugarGuard.Junior.Models.Api;

public sealed class NutritionEntryApiModel
{
    public Guid NutritionEntryId { get; set; }
    public Guid ChildId { get; set; }
    public DateTime RecordedAt { get; set; }
    public MealType MealType { get; set; }
    public string MealName { get; set; } = string.Empty;
    public decimal BreadUnits { get; set; }
    public decimal InsulinUnits { get; set; }
    public decimal? GlucoseBefore { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string MealTypeLabel => MealType switch { MealType.Breakfast => "Завтрак", MealType.Lunch => "Обед", MealType.Dinner => "Ужин", MealType.Snack => "Перекус", _ => "Другое" };
    public string TimeLabel => RecordedAt.ToLocalTime().ToString("HH:mm");
    public string DetailsLabel => $"{BreadUnits:0.##} ХЕ · {InsulinUnits:0.##} ед. инсулина";
}

public sealed class SaveNutritionEntryApiRequest
{
    public DateTime RecordedAt { get; set; }
    public MealType MealType { get; set; }
    public string MealName { get; set; } = string.Empty;
    public decimal BreadUnits { get; set; }
    public decimal InsulinUnits { get; set; }
    public decimal? GlucoseBefore { get; set; }
    public string? Notes { get; set; }
}

public sealed class MealScheduleApiModel
{
    public Guid MealScheduleId { get; set; }
    public Guid ChildId { get; set; }
    public MealType MealType { get; set; }
    public string Title { get; set; } = string.Empty;
    public TimeOnly ScheduledTime { get; set; }
    public decimal? PlannedBreadUnits { get; set; }
    public int DaysOfWeekMask { get; set; }
    public bool ReminderEnabled { get; set; }
    public int ReminderMinutesBefore { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string TimeLabel => ScheduledTime.ToString("HH:mm");
    public string ReminderLabel => ReminderEnabled ? $"Напомнить за {ReminderMinutesBefore} мин." : "Без напоминания";
}

public sealed class SaveMealScheduleApiRequest
{
    public MealType MealType { get; set; }
    public string Title { get; set; } = string.Empty;
    public TimeOnly ScheduledTime { get; set; }
    public decimal? PlannedBreadUnits { get; set; }
    public int DaysOfWeekMask { get; set; } = 127;
    public bool ReminderEnabled { get; set; } = true;
    public int ReminderMinutesBefore { get; set; } = 10;
    public bool IsActive { get; set; } = true;
}

public sealed class NutritionSummaryApiModel
{
    public decimal TotalBreadUnits { get; set; }
    public decimal TotalInsulinUnits { get; set; }
    public decimal AverageBreadUnitsPerDay { get; set; }
    public decimal AverageInsulinUnitsPerDay { get; set; }
    public List<NutritionDailySummaryApiModel> Days { get; set; } = [];
}

public sealed class NutritionDailySummaryApiModel
{
    public DateOnly Date { get; set; }
    public decimal BreadUnits { get; set; }
    public decimal InsulinUnits { get; set; }
    public int EntriesCount { get; set; }
}

public sealed class AchievementApiModel
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageName { get; set; } = string.Empty;
    public int Progress { get; set; }
    public int Target { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public double ProgressValue => Target == 0 ? 0 : Math.Clamp((double)Progress / Target, 0, 1);
    public string ProgressText => $"{Progress} из {Target}";

    // Lightweight vector badges avoid opaque image backgrounds and stay sharp
    // on every child theme.
    public string DisplayImageName => Code switch
    {
        "first_steps" => "badge_first_steps.svg",
        "food_week" => "badge_food_week.svg",
        "insulin_20" => "badge_insulin_20.svg",
        "target_10" => "badge_target_10.svg",
        "schedule_7" => "badge_schedule_7.svg",
        "backpack_10" => "badge_backpack_10.svg",
        _ => "achievement_default.svg"
    };
}
