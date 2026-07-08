using SugarGuard.Domain.Enums;

namespace SugarGuard.Web.ViewModels;

public sealed class NutritionEntryVm
{
    public Guid NutritionEntryId { get; set; }
    public DateTime RecordedAt { get; set; }
    public MealType MealType { get; set; }
    public string MealName { get; set; } = string.Empty;
    public decimal BreadUnits { get; set; }
    public decimal InsulinUnits { get; set; }
    public decimal? GlucoseBefore { get; set; }
    public string? Notes { get; set; }
}

public sealed class SaveNutritionEntryVm
{
    public DateTime RecordedAt { get; set; } = DateTime.Now;
    public MealType MealType { get; set; }
    public string MealName { get; set; } = string.Empty;
    public decimal BreadUnits { get; set; }
    public decimal InsulinUnits { get; set; }
    public decimal? GlucoseBefore { get; set; }
    public string? Notes { get; set; }
}

public sealed class MealScheduleVm
{
    public Guid MealScheduleId { get; set; }
    public MealType MealType { get; set; }
    public string Title { get; set; } = string.Empty;
    public TimeOnly ScheduledTime { get; set; }
    public decimal? PlannedBreadUnits { get; set; }
    public int DaysOfWeekMask { get; set; } = 127;
    public bool ReminderEnabled { get; set; } = true;
    public int ReminderMinutesBefore { get; set; } = 10;
    public bool IsActive { get; set; } = true;
}

public sealed class NutritionSummaryVm
{
    public decimal TotalBreadUnits { get; set; }
    public decimal TotalInsulinUnits { get; set; }
    public decimal AverageBreadUnitsPerDay { get; set; }
    public decimal AverageInsulinUnitsPerDay { get; set; }
    public List<NutritionDailySummaryVm> Days { get; set; } = [];
}

public sealed class NutritionDailySummaryVm
{
    public DateOnly Date { get; set; }
    public decimal BreadUnits { get; set; }
    public decimal InsulinUnits { get; set; }
    public int EntriesCount { get; set; }
}
