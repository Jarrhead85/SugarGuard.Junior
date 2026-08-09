using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using SugarGuard.Junior.Database;
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
    private readonly ISyncService _syncService;
    private string? _childId;
    private Guid? _editingEntryId;
    private Guid? _editingScheduleId;

    public NutritionTrackerPageViewModel(IApiClient apiClient, IStorageService storage, INotificationService notifications, ISyncService syncService)
    {
        _apiClient = apiClient;
        _storage = storage;
        _notifications = notifications;
        _syncService = syncService;
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
    [ObservableProperty] private DateTime entryDate = DateTime.Today;
    [ObservableProperty] private TimeSpan entryTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] private decimal totalBreadUnits;
    [ObservableProperty] private decimal totalInsulinUnits;
    [ObservableProperty] private int totalMeals;
    [ObservableProperty] private bool isType2Profile;
    [ObservableProperty] private string scheduleTitle = string.Empty;
    [ObservableProperty] private TimeSpan scheduleTime = new(8, 0, 0);
    [ObservableProperty] private string plannedBreadUnitsText = string.Empty;
    [ObservableProperty] private bool reminderEnabled = true;
    [ObservableProperty] private int reminderMinutesBefore = 10;
    [ObservableProperty] private bool showEntryForm;
    [ObservableProperty] private bool showScheduleForm;
    [ObservableProperty] private bool scheduleIsNightInsulin;
    [ObservableProperty] private string nightInsulinDoseText = string.Empty;
    [ObservableProperty] private bool showNightInsulinConfirmation;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasEntries => Entries.Count > 0;
    public bool HasSchedules => Schedules.Count > 0;
    public bool HasAchievements => Achievements.Count > 0;
    public MealScheduleApiModel? NightInsulinSchedule => Schedules.FirstOrDefault(item => item.IsNightInsulin && item.IsActive);
    public bool HasNightInsulinSchedule => NightInsulinSchedule is not null;
    public bool HasType1NightInsulinSchedule => ShowType1NutritionFields && HasNightInsulinSchedule;
    public bool ShowType1NutritionFields => !IsType2Profile;
    public bool ShowInsulinSummary => !IsType2Profile;
    public string NutritionSubtitle => IsType2Profile
        ? "Питание, регулярность и самочувствие"
        : "Еда, ХЕ и введённый инсулин";
    public string NutritionPrimaryLabel => IsType2Profile ? "ПРИЁМОВ ПИЩИ" : "СЪЕДЕНО";
    public string NutritionPrimaryValue => IsType2Profile
        ? TotalMeals.ToString(CultureInfo.CurrentCulture)
        : $"{TotalBreadUnits:0.##} ХЕ";
    public string EntryButtonText => _editingEntryId.HasValue ? "Сохранить изменения" : "Добавить в дневник";
    public string ScheduleButtonText => _editingScheduleId.HasValue ? "Сохранить расписание" : "Добавить время";

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsType2ProfileChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowType1NutritionFields));
        OnPropertyChanged(nameof(ShowInsulinSummary));
        OnPropertyChanged(nameof(HasType1NightInsulinSchedule));
        OnPropertyChanged(nameof(NutritionSubtitle));
        OnPropertyChanged(nameof(NutritionPrimaryLabel));
        OnPropertyChanged(nameof(NutritionPrimaryValue));
    }

    partial void OnTotalBreadUnitsChanged(decimal value) => OnPropertyChanged(nameof(NutritionPrimaryValue));
    partial void OnTotalMealsChanged(int value) => OnPropertyChanged(nameof(NutritionPrimaryValue));

    partial void OnScheduleIsNightInsulinChanged(bool value)
    {
        if (!value)
        {
            return;
        }

        ScheduleTitle = "Ночной инсулин";
        PlannedBreadUnitsText = string.Empty;
        SelectedMealTypeIndex = (int)MealType.Other;
    }

    public async Task InitializeAsync()
    {
        _childId = await _storage.GetAsync(AppConstants.StorageKeyCurrentChildId);
        var careMode = await _storage.GetAsync("profile_care_mode");
        var diabetesType = await _storage.GetAsync("diabetes_type");
        IsType2Profile = string.Equals(careMode, "self-managed", StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(diabetesType, "1", StringComparison.Ordinal);
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
            TotalMeals = summary?.Days.Sum(day => day.EntriesCount) ?? 0;
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
        var breadUnits = 0m;
        var insulin = 0m;
        if (ShowType1NutritionFields)
        {
            if (!TryDecimal(BreadUnitsText, out breadUnits) || breadUnits is < 0 or > 50) { ErrorMessage = "ХЕ должны быть числом от 0 до 50."; return; }
            if (!TryDecimal(InsulinUnitsText, out insulin) || insulin is < 0 or > 100) { ErrorMessage = "Инсулин должен быть числом от 0 до 100."; return; }
        }
        decimal? glucose = null;
        if (!string.IsNullOrWhiteSpace(GlucoseBeforeText))
        {
            if (!TryDecimal(GlucoseBeforeText, out var parsed) || parsed is < 1 or > 33) { ErrorMessage = "Сахар до еды должен быть от 1 до 33 ммоль/л."; return; }
            glucose = parsed;
        }

        try
        {
            IsBusy = true; ErrorMessage = string.Empty;
            var request = new SaveNutritionEntryApiRequest
            {
                RecordedAt = DateTime.SpecifyKind(EntryDate.Date.Add(EntryTime), DateTimeKind.Local).ToUniversalTime(),
                MealType = (MealType)SelectedMealTypeIndex,
                MealName = MealName.Trim(),
                BreadUnits = breadUnits, InsulinUnits = insulin, GlucoseBefore = glucose, Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
            };
            var saved = await _apiClient.SaveNutritionEntryAsync(_childId, _editingEntryId, request);
            if (saved is null) { ErrorMessage = "Сервер не сохранил запись. Попробуй ещё раз."; return; }
            ResetEntryForm();
            await LoadAsyncAfterBusy();
        }
        catch (Exception)
        {
            if (!await _syncService.IsConnectedAsync())
            {
                var queued = await _syncService.QueueItemAsync(
                    _editingEntryId?.ToString("D") ?? Guid.NewGuid().ToString("D"),
                    "NutritionEntry",
                    (_editingEntryId.HasValue ? SyncOperationType.Update : SyncOperationType.Insert).ToString(),
                    JsonConvert.SerializeObject(new PendingNutritionEntrySync
                    {
                        ChildId = _childId,
                        NutritionEntryId = _editingEntryId,
                        Request = new SaveNutritionEntryApiRequest
                        {
                            RecordedAt = DateTime.SpecifyKind(EntryDate.Date.Add(EntryTime), DateTimeKind.Local).ToUniversalTime(),
                            MealType = (MealType)SelectedMealTypeIndex,
                            MealName = MealName.Trim(),
                            BreadUnits = breadUnits,
                            InsulinUnits = insulin,
                            GlucoseBefore = glucose,
                            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
                        }
                    }));

                if (queued)
                {
                    ResetEntryForm();
                    ErrorMessage = "Нет сети: запись сохранена на телефоне и будет отправлена автоматически.";
                    return;
                }
            }

            ErrorMessage = "Не удалось сохранить запись.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void EditEntry(NutritionEntryApiModel? entry)
    {
        if (entry is null) return;
        _editingEntryId = entry.NutritionEntryId; SelectedMealTypeIndex = (int)entry.MealType; MealName = entry.MealName;
        var localRecordedAt = entry.RecordedAt.ToLocalTime();
        EntryDate = localRecordedAt.Date;
        EntryTime = localRecordedAt.TimeOfDay;
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
    private void ToggleScheduleForm()
    {
        ShowScheduleForm = !ShowScheduleForm;
        if (ShowScheduleForm && !_editingScheduleId.HasValue)
        {
            ScheduleIsNightInsulin = false;
        }
    }

    [RelayCommand]
    private void ToggleNightInsulinConfirmation() => ShowNightInsulinConfirmation = !ShowNightInsulinConfirmation;

    [RelayCommand]
    private async Task ConfirmNightInsulinAsync()
    {
        if (string.IsNullOrWhiteSpace(_childId) || NightInsulinSchedule is null || IsBusy) return;
        if (!TryDecimal(NightInsulinDoseText, out var dose) || dose is <= 0 or > 100)
        {
            ErrorMessage = "Укажи фактически введённую дозу от 0,1 до 100 ед.";
            return;
        }

        var request = new SaveNutritionEntryApiRequest
        {
            RecordedAt = DateTime.UtcNow, MealType = MealType.Other, MealName = "Ночной инсулин",
            BreadUnits = 0, InsulinUnits = dose, Notes = "Подтверждено ребёнком в мобильном приложении."
        };
        try
        {
            IsBusy = true;
            var saved = await _apiClient.SaveNutritionEntryAsync(_childId, null, request);
            if (saved is null) throw new InvalidOperationException();
            await CancelNightInsulinRemindersAsync(NightInsulinSchedule);
            NightInsulinDoseText = string.Empty;
            ShowNightInsulinConfirmation = false;
            await LoadAsyncAfterBusy();
        }
        catch (Exception)
        {
            if (!await _syncService.IsConnectedAsync() && await _syncService.QueueItemAsync(
                    Guid.NewGuid().ToString("D"), "NutritionEntry", SyncOperationType.Insert.ToString(),
                    JsonConvert.SerializeObject(new PendingNutritionEntrySync { ChildId = _childId, Request = request })))
            {
                await CancelNightInsulinRemindersAsync(NightInsulinSchedule);
                NightInsulinDoseText = string.Empty;
                ShowNightInsulinConfirmation = false;
                ErrorMessage = "Нет сети: подтверждение сохранено на телефоне и будет отправлено автоматически.";
            }
            else ErrorMessage = "Не удалось подтвердить ночной укол.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (string.IsNullOrWhiteSpace(_childId) || IsBusy) return;
        if (IsType2Profile)
            ScheduleIsNightInsulin = false;
        if (string.IsNullOrWhiteSpace(ScheduleTitle) && !ScheduleIsNightInsulin) { ErrorMessage = "Укажи название приёма пищи."; return; }
        decimal? planned = null;
        if (!string.IsNullOrWhiteSpace(PlannedBreadUnitsText))
        {
            if (!TryDecimal(PlannedBreadUnitsText, out var parsed) || parsed is < 0 or > 50) { ErrorMessage = "План ХЕ должен быть от 0 до 50."; return; }
            planned = parsed;
        }
        try
        {
            IsBusy = true; ErrorMessage = string.Empty;
            var mealType = ScheduleIsNightInsulin ? MealType.Other : InferMealType(ScheduleTitle, (MealType)SelectedMealTypeIndex);
            var result = await _apiClient.SaveMealScheduleAsync(_childId, _editingScheduleId, new SaveMealScheduleApiRequest
            {
                MealType = mealType, Title = ScheduleIsNightInsulin ? "Ночной инсулин" : ScheduleTitle.Trim(), ScheduledTime = TimeOnly.FromTimeSpan(ScheduleTime),
                PlannedBreadUnits = planned, DaysOfWeekMask = 127, ReminderEnabled = ReminderEnabled,
                ReminderMinutesBefore = Math.Clamp(ReminderMinutesBefore, 0, 180), IsActive = true,
                IsNightInsulin = ScheduleIsNightInsulin, RepeatIntervalMinutes = 5, EscalationWindowMinutes = 60
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
        ReminderEnabled = schedule.ReminderEnabled; ReminderMinutesBefore = schedule.ReminderMinutesBefore; ScheduleIsNightInsulin = schedule.IsNightInsulin; ShowScheduleForm = true;
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
            if (schedule.IsNightInsulin)
            {
                if (!IsType2Profile)
                    await ScheduleNightInsulinRemindersAsync(schedule);
                continue;
            }

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

    private async Task ScheduleNightInsulinRemindersAsync(MealScheduleApiModel schedule)
    {
        for (var day = 0; day < 7; day++)
        {
            var scheduledFor = DateTime.Today.AddDays(day).Add(schedule.ScheduledTime.ToTimeSpan());
            var reminder = scheduledFor.AddMinutes(-schedule.ReminderMinutesBefore);
            if (reminder > DateTime.Now) await _notifications.ScheduleNotificationAsync("Ночной инсулин", "Подготовься к ночному уколу и после введения укажи фактическую дозу.", NightReminderId(schedule, day, 0), reminder);
            var repeats = Math.Max(1, schedule.EscalationWindowMinutes / Math.Max(1, schedule.RepeatIntervalMinutes));
            for (var repeat = 0; repeat <= repeats; repeat++)
            {
                var at = scheduledFor.AddMinutes(repeat * schedule.RepeatIntervalMinutes);
                if (at > DateTime.Now) await _notifications.ScheduleNotificationAsync("Ночной инсулин — требуется подтверждение", "Укажи фактически введённую дозу. Напоминания повторяются до подтверждения.", NightReminderId(schedule, day, repeat + 1), at);
            }
        }
    }

    private async Task CancelNightInsulinRemindersAsync(MealScheduleApiModel schedule)
    {
        for (var day = 0; day < 7; day++)
        for (var repeat = 0; repeat <= schedule.EscalationWindowMinutes / Math.Max(1, schedule.RepeatIntervalMinutes) + 1; repeat++)
            await _notifications.CancelNotificationAsync(NightReminderId(schedule, day, repeat));
    }

    private static string NightReminderId(MealScheduleApiModel schedule, int day, int repeat) => $"night_insulin_{schedule.MealScheduleId:N}_{day}_{repeat}";

    private void BuildDays(IEnumerable<NutritionDailySummaryApiModel> source)
    {
        var rows = source.OrderBy(item => item.Date).TakeLast(7).ToList();
        var max = Math.Max(1m, rows.SelectMany(item => new[] { item.BreadUnits, item.InsulinUnits }).DefaultIfEmpty(1).Max());
        Replace(Days, rows.Select(item => new NutritionDayDisplay(item.Date.ToString("dd.MM"), item.BreadUnits, item.InsulinUnits, (double)(item.BreadUnits / max), (double)(item.InsulinUnits / max))));
    }

    private void ResetEntryForm() { _editingEntryId = null; MealName = BreadUnitsText = InsulinUnitsText = GlucoseBeforeText = Notes = string.Empty; EntryDate = DateTime.Today; EntryTime = DateTime.Now.TimeOfDay; ShowEntryForm = false; OnPropertyChanged(nameof(EntryButtonText)); }
    private void ResetScheduleForm() { _editingScheduleId = null; ScheduleTitle = PlannedBreadUnitsText = string.Empty; ScheduleTime = new TimeSpan(8, 0, 0); ScheduleIsNightInsulin = false; ShowScheduleForm = false; OnPropertyChanged(nameof(ScheduleButtonText)); }
    private void NotifyCollections() { OnPropertyChanged(nameof(HasEntries)); OnPropertyChanged(nameof(HasSchedules)); OnPropertyChanged(nameof(HasAchievements)); OnPropertyChanged(nameof(NightInsulinSchedule)); OnPropertyChanged(nameof(HasNightInsulinSchedule)); OnPropertyChanged(nameof(HasType1NightInsulinSchedule)); }
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
