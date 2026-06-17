// ViewModel для страницы управления расписанием измерений
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Services.Interfaces;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel для управления расписанием измерений глюкозы
/// Позволяет добавлять, удалять и редактировать времена измерений
/// </summary>
public partial class SchedulePageViewModel : ObservableObject
{
    private readonly IScheduleService _scheduleService;
    private readonly ILogger<SchedulePageViewModel> _logger;
    private readonly IChildRepository _childRepository;
    private readonly IStorageService _storageService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddTimeCommand))]
    private bool isLoading;

    [ObservableProperty]
    private string newTimeText = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    private string? _currentChildId;

    public SchedulePageViewModel(
        IScheduleService scheduleService,
        ILogger<SchedulePageViewModel> logger,
        IChildRepository childRepository,
        IStorageService storageService)
    {
        _scheduleService = scheduleService;
        _logger = logger;
        _childRepository = childRepository;
        _storageService = storageService;
        ScheduleItems = new ObservableCollection<MeasurementSchedule>();
    }

    /// <summary>
    /// Список времён измерений
    /// </summary>
    public ObservableCollection<MeasurementSchedule> ScheduleItems { get; }

    /// <summary>
    /// Есть ли элементы в расписании
    /// </summary>
    public bool HasScheduleItems => ScheduleItems.Any();

    /// <summary>
    /// Следующее запланированное измерение
    /// </summary>
    public string NextMeasurementText
    {
        get
        {
            if (!HasScheduleItems) return "Расписание пустое";

            var now = TimeOnly.FromDateTime(DateTime.Now);
            var nextItem = ScheduleItems
                .Where(s => s.IsActive)
                .Where(s => s.ScheduledTime > now)
                .OrderBy(s => s.ScheduledTime)
                .FirstOrDefault();

            if (nextItem == null)
            {
                nextItem = ScheduleItems
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.ScheduledTime)
                    .FirstOrDefault();

                return nextItem != null ? $"Завтра в {nextItem.FormattedTime}" : "Нет активных времён";
            }

            return $"Сегодня в {nextItem.FormattedTime}";
        }
    }

    partial void OnNewTimeTextChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    private bool CanAddTime() => !IsLoading;

    /// <summary>
    /// Инициализация страницы - загружает расписание для текущего ребёнка из storage
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _currentChildId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);
            if (string.IsNullOrEmpty(_currentChildId))
            {
                _logger.LogInformation("Текущий ребёнок не выбран — расписание не загружено");
                ErrorMessage = "Выберите ребёнка в профиле";
                return;
            }
            await LoadScheduleAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации страницы расписания");
            ErrorMessage = "Ошибка загрузки данных";
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddTime))]
    private async Task AddTimeAsync()
    {
        try
        {
            ErrorMessage = string.Empty;

            if (!TimeOnly.TryParseExact(NewTimeText, "HH:mm", out var time))
            {
                ErrorMessage = "Неверный формат времени. Используйте HH:MM (например: 08:30)";
                return;
            }

            if (string.IsNullOrEmpty(_currentChildId))
            {
                ErrorMessage = "Выберите ребёнка в профиле";
                return;
            }

            var isValid = await _scheduleService.IsValidScheduleTimeAsync(_currentChildId, time);
            if (!isValid)
            {
                ErrorMessage = "Это время уже добавлено в расписание";
                return;
            }

            IsLoading = true;

            var success = await _scheduleService.AddScheduleItemAsync(_currentChildId, time);

            if (success)
            {
                NewTimeText = string.Empty;
                await LoadScheduleAsync();
                _logger.LogInformation("Добавлено время {Time}", time.ToString("HH:mm"));
            }
            else
            {
                ErrorMessage = "Не удалось добавить время";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка добавления времени {Time}", NewTimeText);
            ErrorMessage = "Ошибка добавления времени";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveTimeAsync(MeasurementSchedule? item)
    {
        if (item == null) return;
        if (string.IsNullOrEmpty(_currentChildId)) return;

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var success = await _scheduleService.RemoveScheduleItemAsync(_currentChildId, item.ScheduledTime);

            if (success)
            {
                ScheduleItems.Remove(item);
                OnPropertyChanged(nameof(HasScheduleItems));
                OnPropertyChanged(nameof(NextMeasurementText));
                _logger.LogInformation("Удалено время {Time}", item.FormattedTime);
            }
            else
            {
                ErrorMessage = "Не удалось удалить время";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления времени {Time}", item.FormattedTime);
            ErrorMessage = "Ошибка удаления времени";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(MeasurementSchedule? item)
    {
        if (item == null) return;

        try
        {
            var newStatus = !item.IsActive;
            var success = await _scheduleService.SetScheduleItemActiveAsync(item.ScheduleId, newStatus);

            if (success)
            {
                item.IsActive = newStatus;
                OnPropertyChanged(nameof(NextMeasurementText));
                _logger.LogInformation(" Изменён статус времени {Time}: {Status}",
                    item.FormattedTime, newStatus ? "активно" : "неактивно");
            }
            else
            {
                ErrorMessage = "Не удалось изменить статус";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка изменения статуса времени {Time}", item.FormattedTime);
            ErrorMessage = "Ошибка изменения статуса";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadScheduleAsync();
    }

    private async Task LoadScheduleAsync()
    {
        if (string.IsNullOrEmpty(_currentChildId)) return;
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var schedules = await _scheduleService.GetScheduleAsync(_currentChildId);

            ScheduleItems.Clear();
            foreach (var schedule in schedules)
            {
                ScheduleItems.Add(schedule);
            }

            OnPropertyChanged(nameof(HasScheduleItems));
            OnPropertyChanged(nameof(NextMeasurementText));

            _logger.LogInformation("Загружено {Count} времён измерений", schedules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки расписания");
            ErrorMessage = "Ошибка загрузки расписания";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
