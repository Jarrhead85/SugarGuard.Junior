using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SugarGuard.Domain.Enums;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Services.Interfaces;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.ViewModels;

public partial class NutritionTrackerPageViewModel : ObservableObject
{
    private readonly IApiClient _apiClient;
    private readonly IStorageService _storage;
    private readonly INotificationService _notifications;
    private string? _childId;
    private Guid? _editingEntryId;
    private Guid? _editingScheduleId;

    public NutritionTrackerPageViewModel(IApiClient apiClient, IStorageService storage, INotificationService notifications)
    {
        _apiClient = apiClient;
        _storage = storage;
        _notifications = notifications;
    }

    public IReadOnlyList<string> MealTypeOptions { get; } = ["Завтрак", "Обед", "Ужин", "Перекус", "Другое"];
    public ObservableCollection<NutritionEntryApiModel> Entries { get; } = [];
    public ObservableCollection<MealScheduleApiModel> Schedules { get; } = [];
    public ObservableCollection<AchievementApiModel> Achievements { get; } = [];
    public ObservableCollection<NutritionDayDisplay> Days { get; } = [];

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private int selectedMealTypeIndex;
    [ObservableProperty] private string mealName = string.Empty;
    [ObservableProperty] private string breadUnitsText = string.Empty;
    [ObservableProperty] private string insulinUnitsText = string.Empty;
    [ObservableProperty] private string glucoseBeforeText = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private decimal totalBreadUnits;
    [ObservableProperty] private decimal totalInsulinUnits;
    [ObservableProperty] private string scheduleTitle = string.Empty;
    [ObservableProperty] private TimeSpan scheduleTime = new(8, 0, 0);
    [ObservableProperty] private string plannedBreadUnitsText = string.Empty;
    [ObservableProperty] private bool reminderEnabled = true;
    [ObservableProperty] private int reminderMinutesBefore = 10;
    [ObservableProperty] private bool showEntryForm;
    [ObservableProperty] private bool showScheduleForm;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasEntries => Entries.Count > 0;
    public bool HasSchedules => Schedules.Count > 0;
    public bool HasAchievements => Achievements.Count > 0;
    public string EntryButtonText => _editingEntryId.HasValue ? "Сохранить изменения" : "Добавить в дневник";
    public string ScheduleButtonText => _editingScheduleId.HasValue ? "Сохранить расписание" : "Добавить время";

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    public async Task InitializeAsync()
    {
        _childId = await _storage.GetAsync(AppConstants.StorageKeyCurrentChildId);
        if (string.IsNullOrWhiteSpace(_childId)) { ErrorMessage = "Профиль ребёнка ещё не выбран."; return; }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(_childId)) return;
        try
        {
            IsBusy = true; ErrorMessage = string.Empty;
            var from = DateTime.Today.AddDays(-29);
            var to = DateTime.Today.AddDays(1).AddTicks(-1);
            var entriesTask = _apiClient.GetNutritionEntriesAsync(_childId, from, to);
            var schedulesTask = _apiClient.GetMealScheduleAsync(_childId);
            var summaryTask = _apiClient.GetNutritionSummaryAsync(_childId, from, to);
            var achievementsTask = _apiClient.GetAchievementsAsync(_childId);
            await Task.WhenAll(entriesTask, schedulesTask, summaryTask, achievementsTask);

            Replace(Entries, entriesTask.Result.Take(30));
            Replace(Schedules, schedulesTask.Result);
            Replace(Achievements, achievementsTask.Result);
            var summary = summaryTask.Result;
            TotalBreadUnits = summary?.TotalBreadUnits ?? 0;
            TotalInsulinUnits = summary?.TotalInsulinUnits ?? 0;
            BuildDays(summary?.Days ?? []);
            NotifyCollections();
            await ScheduleMealRemindersAsync(Schedules);
        }
        catch (Exception)
        {
            ErrorMessage = "Не удалось загрузить дневник. Проверь подключение к интернету.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ToggleEntryForm() => ShowEntryForm = !ShowEntryForm;

    [RelayCommand]
    private async Task SaveEntryAsync()
    {
        if (string.IsNullOrWhiteSpace(_childId) || IsBusy) return;
        if (string.IsNullOrWhiteSpace(MealName)) { ErrorMessage = "Укажи, что было съедено."; return; }
        if (!TryDecimal(BreadUnitsText, out var breadUnits) || breadUnits is < 0 or > 50) { ErrorMessage = "ХЕ должны быть числом от 0 до 50."; return; }
        if (!TryDecimal(InsulinUnitsText, out var insulin) || insulin is < 0 or > 100) { ErrorMessage = "Инсулин должен быть числом от 0 до 100."; return; }
        decimal? glucose = null;
        if (!string.IsNullOrWhiteSpace(GlucoseBeforeText))
        {
            if (!TryDecimal(GlucoseBeforeText, out var parsed) || parsed is < 1 or > 33) { ErrorMessage = "Сахар до еды должен быть от 1 до 33 ммоль/л."; return; }
            glucose = parsed;
        }

        try
        {
            IsBusy = true; ErrorMessage = string.Empty;
            var saved = await _apiClient.SaveNutritionEntryAsync(_childId, _editingEntryId, new SaveNutritionEntryApiRequest
            {
                RecordedAt = DateTime.Now.ToUniversalTime(), MealType = (MealType)SelectedMealTypeIndex, MealName = MealName.Trim(),
                BreadUnits = breadUnits, InsulinUnits = insulin, GlucoseBefore = glucose, Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
            });
            if (saved is null) { ErrorMessage = "Сервер не сохранил запись. Попробуй ещё раз."; return; }
            ResetEntryForm();
            await LoadAsyncAfterBusy();
        }
        catch (Exception) { ErrorMessage = "Не удалось сохранить запись."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void EditEntry(NutritionEntryApiModel? entry)
    {
        if (entry is null) return;
        _editingEntryId = entry.NutritionEntryId; SelectedMealTypeIndex = (int)entry.MealType; MealName = entry.MealName;
        BreadUnitsText = entry.BreadUnits.ToString("0.##", CultureInfo.CurrentCulture); InsulinUnitsText = entry.InsulinUnits.ToString("0.##", CultureInfo.CurrentCulture);
        GlucoseBeforeText = entry.GlucoseBefore?.ToString("0.0", CultureInfo.CurrentCulture) ?? string.Empty; Notes = entry.Notes ?? string.Empty; ShowEntryForm = true;
        OnPropertyChanged(nameof(EntryButtonText));
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(NutritionEntryApiModel? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(_childId)) return;
        if (await _apiClient.DeleteNutritionEntryAsync(_childId, entry.NutritionEntryId)) await LoadAsync();
    }

    [RelayCommand]
    private void ToggleScheduleForm() => ShowScheduleForm = !ShowScheduleForm;

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (string.IsNullOrWhiteSpace(_childId) || IsBusy) return;
        if (string.IsNullOrWhiteSpace(ScheduleTitle)) { ErrorMessage = "Укажи название приёма пищи."; return; }
        decimal? planned = null;
        if (!string.IsNullOrWhiteSpace(PlannedBreadUnitsText))
        {
            if (!TryDecimal(PlannedBreadUnitsText, out var parsed) || parsed is < 0 or > 50) { ErrorMessage = "План ХЕ должен быть от 0 до 50."; return; }
            planned = parsed;
        }
        try
        {
            IsBusy = true; ErrorMessage = string.Empty;
            var mealType = InferMealType(ScheduleTitle, (MealType)SelectedMealTypeIndex);
            var result = await _apiClient.SaveMealScheduleAsync(_childId, _editingScheduleId, new SaveMealScheduleApiRequest
            {
                MealType = mealType, Title = ScheduleTitle.Trim(), ScheduledTime = TimeOnly.FromTimeSpan(ScheduleTime),
                PlannedBreadUnits = planned, DaysOfWeekMask = 127, ReminderEnabled = ReminderEnabled,
                ReminderMinutesBefore = Math.Clamp(ReminderMinutesBefore, 0, 180), IsActive = true
            });
            if (result is null) { ErrorMessage = "Не удалось сохранить расписание."; return; }
            ResetScheduleForm(); await LoadAsyncAfterBusy();
        }
        catch (Exception) { ErrorMessage = "Не удалось сохранить расписание."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void EditSchedule(MealScheduleApiModel? schedule)
    {
        if (schedule is null) return;
        _editingScheduleId = schedule.MealScheduleId; SelectedMealTypeIndex = (int)schedule.MealType; ScheduleTitle = schedule.Title;
        ScheduleTime = schedule.ScheduledTime.ToTimeSpan(); PlannedBreadUnitsText = schedule.PlannedBreadUnits?.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;
        ReminderEnabled = schedule.ReminderEnabled; ReminderMinutesBefore = schedule.ReminderMinutesBefore; ShowScheduleForm = true;
        OnPropertyChanged(nameof(ScheduleButtonText));
    }

    [RelayCommand]
    private async Task DeleteScheduleAsync(MealScheduleApiModel? schedule)
    {
        if (schedule is null || string.IsNullOrWhiteSpace(_childId)) return;
        if (await _apiClient.DeleteMealScheduleAsync(_childId, schedule.MealScheduleId)) await LoadAsync();
    }

    [RelayCommand]
    private async Task ExportAsync(string? format)
    {
        if (string.IsNullOrWhiteSpace(_childId)) return;
        var safeFormat = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "pdf";
        var bytes = await _apiClient.ExportNutritionAsync(_childId, DateTime.Today.AddDays(-29), DateTime.Today.AddDays(1).AddTicks(-1), safeFormat);
        if (bytes.Length == 0) { ErrorMessage = "Не удалось сформировать файл."; return; }
        var path = Path.Combine(FileSystem.CacheDirectory, $"sugarguard-diary-{DateTime.Now:yyyyMMdd}.{safeFormat}");
        await File.WriteAllBytesAsync(path, bytes);
        await Share.Default.RequestAsync(new ShareFileRequest("Дневник SugarGuard", new ShareFile(path)));
    }

    private async Task LoadAsyncAfterBusy() { IsBusy = false; await LoadAsync(); IsBusy = true; }

    private async Task ScheduleMealRemindersAsync(IEnumerable<MealScheduleApiModel> schedules)
    {
        foreach (var schedule in schedules)
        {
            for (var day = 0; day < 14; day++)
            {
                var id = $"meal_{schedule.MealScheduleId:N}_{day}";
                await _notifications.CancelNotificationAsync(id);
                var date = DateTime.Today.AddDays(day);
                var bit = 1 << (int)date.DayOfWeek;
                if (!schedule.IsActive || !schedule.ReminderEnabled || (schedule.DaysOfWeekMask & bit) == 0) continue;
                var at = date.Add(schedule.ScheduledTime.ToTimeSpan()).AddMinutes(-schedule.ReminderMinutesBefore);
                if (at > DateTime.Now) await _notifications.ScheduleNotificationAsync("Пора по расписанию", $"Скоро {schedule.Title.ToLowerInvariant()}. Не забудь записать ХЕ и инсулин.", id, at);
            }
        }
    }

    private void BuildDays(IEnumerable<NutritionDailySummaryApiModel> source)
    {
        var rows = source.OrderBy(item => item.Date).TakeLast(7).ToList();
        var max = Math.Max(1m, rows.SelectMany(item => new[] { item.BreadUnits, item.InsulinUnits }).DefaultIfEmpty(1).Max());
        Replace(Days, rows.Select(item => new NutritionDayDisplay(item.Date.ToString("dd.MM"), item.BreadUnits, item.InsulinUnits, (double)(item.BreadUnits / max), (double)(item.InsulinUnits / max))));
    }

    private void ResetEntryForm() { _editingEntryId = null; MealName = BreadUnitsText = InsulinUnitsText = GlucoseBeforeText = Notes = string.Empty; ShowEntryForm = false; OnPropertyChanged(nameof(EntryButtonText)); }
    private void ResetScheduleForm() { _editingScheduleId = null; ScheduleTitle = PlannedBreadUnitsText = string.Empty; ScheduleTime = new TimeSpan(8, 0, 0); ShowScheduleForm = false; OnPropertyChanged(nameof(ScheduleButtonText)); }
    private void NotifyCollections() { OnPropertyChanged(nameof(HasEntries)); OnPropertyChanged(nameof(HasSchedules)); OnPropertyChanged(nameof(HasAchievements)); }
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source) { target.Clear(); foreach (var item in source) target.Add(item); }
    private static bool TryDecimal(string text, out decimal value) => decimal.TryParse(text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private static MealType InferMealType(string? title, MealType fallback)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return fallback;
        }

        var normalized = title.Trim().ToLowerInvariant();
        if (normalized.Contains("обед", StringComparison.OrdinalIgnoreCase))
        {
            return MealType.Lunch;
        }

        if (normalized.Contains("ужин", StringComparison.OrdinalIgnoreCase))
        {
            return MealType.Dinner;
        }

        if (normalized.Contains("перекус", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("полдник", StringComparison.OrdinalIgnoreCase))
        {
            return MealType.Snack;
        }

        if (normalized.Contains("завтрак", StringComparison.OrdinalIgnoreCase))
        {
            return MealType.Breakfast;
        }

        return fallback;
    }
}

public sealed record NutritionDayDisplay(string Date, decimal BreadUnits, decimal InsulinUnits, double BreadUnitsProgress, double InsulinUnitsProgress);
