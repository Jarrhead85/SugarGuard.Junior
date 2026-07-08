using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Domain.Enums;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Views.Components;
using System.Collections.ObjectModel;
using System.Globalization;
using SugarGuard.Junior.Utilities;
using SugarGuard.Junior.Core.Validation;
using SugarGuard.Shared.Constants;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel главной страницы (Dashboard).
/// Отвечает за: последнее измерение глюкозы, тренд, мини-график,
/// суммарные ХЕ рюкзака, статус синхронизации и навигацию.
/// </summary>
public partial class MainPageViewModel : ObservableObject, IDisposable
{
    // ========== ЗАВИСИМОСТИ ==========
    private readonly IMeasurementService _measurementService;
    private readonly ISyncService _syncService;
    private readonly IRecommendationModalViewModelFactory _recommendationModalViewModelFactory;
    private readonly IRecommendationModalFactory _recommendationModalFactory;
    private readonly ILogger<MainPageViewModel> _logger;
    private readonly ICryptoService _cryptoService;
    private readonly IStorageService _storageService;
    private readonly IMeasurementRepository _measurementRepository;
    private readonly IBackpackRepository _backpackRepository;
    private readonly IChildRepository _childRepository;
    private readonly IDiabetesSettingsRepository _diabetesSettingsRepository;

    /// <summary>
    /// ID текущего ребёнка — загружается из хранилища при инициализации.
    /// </summary>
    private string? _currentChildId;

    // ========== ПРИВЯЗАННЫЕ СВОЙСТВА: ИЗМЕРЕНИЯ ==========

    [ObservableProperty]
    private ObservableCollection<MeasurementEntity> measurements = new();

    [ObservableProperty]
    private string newGlucoseValue = string.Empty;

    partial void OnNewGlucoseValueChanged(string value)
    {
        var normalized = GlucoseInputNormalizer.Normalize(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            NewGlucoseValue = normalized;
        }
    }

    [ObservableProperty]
    private string lastGlucoseValue = "—";

    [ObservableProperty]
    private string lastMeasurementTime = "—";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isLoading = false;

    /// <summary>
    /// Сбрасываем CanExecute у команды отправки при изменении флага загрузки.
    /// </summary>
    partial void OnIsLoadingChanged(bool value)
    {
        if (SendMeasurementCommand is AsyncRelayCommand cmd)
        {
            cmd.NotifyCanExecuteChanged();
        }
    }

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isErrorVisible = false;

    [ObservableProperty]
    private RecommendationResponse? currentRecommendation = null;

    [ObservableProperty]
    private bool isRecommendationModalOpen = false;

    // ========== ПРИВЯЗАННЫЕ СВОЙСТВА: ПРОФИЛЬ И ДАТА ==========

    [ObservableProperty]
    private string childName = "друг";

    [ObservableProperty]
    private string glucoseShortcut1 = "4";

    [ObservableProperty]
    private string glucoseShortcut2 = "6";

    [ObservableProperty]
    private string glucoseShortcut3 = "8";

    [ObservableProperty]
    private string glucoseShortcut4 = "10";

    [ObservableProperty]
    private string currentDate = DateTime.Now.ToString("dd MMMM yyyy");

    [ObservableProperty]
    private string currentTime = DateTime.Now.ToString("HH:mm");

    // ========== ПРИВЯЗАННЫЕ СВОЙСТВА: СИНХРОНИЗАЦИЯ ==========

    [ObservableProperty]
    private string syncStatus = "Синхронизировано";

    // ========== ПРИВЯЗАННЫЕ СВОЙСТВА: ТРЕНД И СОСТОЯНИЕ ==========

    [ObservableProperty]
    private string trendArrow = "→";

    [ObservableProperty]
    private bool showRecommendation = false;

    [ObservableProperty]
    private string recommendationUrgency = string.Empty;

    [ObservableProperty]
    private ChildState selectedChildState = ChildState.Normal;

    /// <summary>
    /// Человекочитаемые названия состояний ребёнка для Picker в UI.
    /// Порядок строго соответствует значениям enum ChildState (0..9).
    /// </summary>
    public IList<string> ChildStateDisplayNames { get; } = new List<string>
    {
        "Не указано", "Норма", "Проснулся", "До еды",
        "После еды", "Спорт", "Перед сном", "Ночь", "Гипо", "Гипер"
    };

    [ObservableProperty]
    private int selectedChildStateIndex = 1;

    /// <summary>
    /// Синхронизируем индекс Picker с enum ChildState.
    /// </summary>
    partial void OnSelectedChildStateIndexChanged(int value)
    {
        if (value >= 0 && value <= (int)ChildState.Hyperglycemia)
            SelectedChildState = (ChildState)value;
    }

    /// <summary>
    /// Команда для быстрых кнопок-шоткатов ввода значения глюкозы (3.5, 5.0, 7.0 и т.д.).
    /// </summary>
    [RelayCommand]
    private void SetGlucoseShortcut(string value)
    {
        if (!string.IsNullOrEmpty(value))
            NewGlucoseValue = value;
    }

    // ========== ПРИВЯЗАННЫЕ СВОЙСТВА: СЕМАНТИЧЕСКИЕ КЛЮЧИ СОСТОЯНИЙ ==========

    /// <summary>
    /// Семантический ключ цвета срочности рекомендации.
    /// Допустимые значения: "Primary" | "Warning" | "Danger".
    /// В XAML преобразуется через StatusToColorConverter — hex в коде не хранится.
    /// </summary>
    [ObservableProperty]
    private string urgencyKey = "Primary";

    [ObservableProperty]
    private string recommendationText = string.Empty;

    [ObservableProperty]
    private string glucoseHeroAccessibilityText = string.Empty;

    [ObservableProperty]
    private string recommendationUrgencyAccessibilityText = string.Empty;

    // ========== ПРИВЯЗАННЫЕ СВОЙСТВА: МИНИ-ГРАФИК ==========

    /// <summary>
    /// Drawable для мини-графика на главном экране.
    /// Переиспользует ChartDrawable с флагом IsMiniMode = true.
    /// </summary>
    public GlucoseChartDrawable MiniChartDrawable { get; } = new GlucoseChartDrawable();

    /// <summary>
    /// Показывать GraphicsView мини-графика только при наличии хотя бы 2 точек.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChart))]
    [NotifyPropertyChangedFor(nameof(HasMeasurements))]
    private ObservableCollection<ChartDataPoint> miniChartPoints = new();

    /// <summary>
    /// True, если есть достаточно данных для отрисовки мини-графика (≥2 точек).
    /// </summary>
    public bool ShowChart => MiniChartPoints.Count >= 2;

    public bool HasMeasurements => MiniChartPoints.Count >= 1;

    // ========== ПРИВЯЗАННЫЕ СВОЙСТВА: СТАТИСТИКА ==========

    [ObservableProperty]
    private double timeInTargetRange = 0.0;

    [ObservableProperty]
    private string dailyStats = "Сегодня: нет измерений";

    /// <summary>
    /// Суммарные хлебные единицы из рюкзака. Загружаются в OnLoadDataAsync.
    /// </summary>
    [ObservableProperty]
    private double totalBreadUnits = 0.0;

    // ========== ПРИВЯЗАННЫЕ СВОЙСТВА: ОФЛАЙН-СТАТУС ==========

    [ObservableProperty]
    private bool isOffline = false;

    [ObservableProperty]
    private int pendingSyncCount = 0;

    [ObservableProperty]
    private string offlineStatusText = string.Empty;

    [ObservableProperty]
    private bool showOfflineIndicator = false;

    // ========== ПРИВЯЗАННЫЕ СВОЙСТВА: КРИТИЧНОСТЬ И UI-СОСТОЯНИЕ ==========

    [ObservableProperty]
    private bool isCritical = false;

    [ObservableProperty]
    private GlucoseUiState currentGlucoseUiState = GlucoseUiState.Normal;

    [ObservableProperty]
    private decimal lastGlucoseValueDecimal = 0m;

    /// <summary>
    /// True, если ChildId не выбран — показываем блок "выберите ребёнка" на главной.
    /// </summary>
    [ObservableProperty]
    private bool showNoChildSelected = false;

    /// <summary>
    /// Показывать блок рюкзака на главной (есть ХЕ).
    /// </summary>
    [ObservableProperty]
    private bool showBackpackSummary = false;

    /// <summary>
    /// Инверсия IsLoading для привязок CanExecute и IsEnabled.
    /// </summary>
    public bool IsNotBusy => !IsLoading;

    // ========== КОМАНДЫ ==========

    public IAsyncRelayCommand SendMeasurementCommand { get; }
    public IAsyncRelayCommand CloseModalCommand { get; }
    public IAsyncRelayCommand LoadDataCommand { get; }
    public IAsyncRelayCommand GoToProfileCommand { get; }
    public IAsyncRelayCommand NavigateToHelpCommand { get; }
    public IAsyncRelayCommand NavigateToChartCommand { get; }
    public IAsyncRelayCommand NavigateToScheduleCommand { get; }
    public IAsyncRelayCommand NavigateToBackpackCommand { get; }

    // ========== КОНСТРУКТОР ==========

    public MainPageViewModel(
        IMeasurementService measurementService,
        ISyncService syncService,
        IRecommendationModalViewModelFactory recommendationModalViewModelFactory,
        IRecommendationModalFactory recommendationModalFactory,
        ILogger<MainPageViewModel> logger,
        ICryptoService cryptoService,
        IStorageService storageService,
        IMeasurementRepository measurementRepository,
        IBackpackRepository backpackRepository,
        IChildRepository childRepository,
        IDiabetesSettingsRepository diabetesSettingsRepository)
    {
        _measurementService = measurementService;
        _syncService = syncService;
        _recommendationModalViewModelFactory = recommendationModalViewModelFactory;
        _recommendationModalFactory = recommendationModalFactory;
        _logger = logger;
        _cryptoService = cryptoService;
        _storageService = storageService;
        _measurementRepository = measurementRepository;
        _backpackRepository = backpackRepository;
        _childRepository = childRepository;
        _diabetesSettingsRepository = diabetesSettingsRepository;

        SendMeasurementCommand = new AsyncRelayCommand(OnSubmitMeasurementAsync);
        CloseModalCommand = new AsyncRelayCommand(OnCloseModalAsync);
        LoadDataCommand = new AsyncRelayCommand(OnLoadDataAsync);
        GoToProfileCommand = new AsyncRelayCommand(GoToProfileAsync);
        NavigateToHelpCommand = new AsyncRelayCommand(NavigateToHelpAsync);
        NavigateToChartCommand = new AsyncRelayCommand(NavigateToChartAsync);
        NavigateToScheduleCommand = new AsyncRelayCommand(NavigateToScheduleAsync);
        NavigateToBackpackCommand = new AsyncRelayCommand(NavigateToBackpackAsync);

        // Подписываемся на события MAUI Connectivity API
        Connectivity.Current.ConnectivityChanged += OnPlatformConnectivityChanged;

        // Подписываемся на события синхронизации
        _syncService.ConnectivityChanged += OnConnectivityChanged;
        _syncService.SyncStarted += OnSyncStarted;
        _syncService.SyncCompleted += OnSyncCompleted;
    }

    // ========== ИНИЦИАЛИЗАЦИЯ ==========

    /// <summary>
    /// Вызывается из MainPage.OnAppearing.
    /// Загружает ChildId, инициализирует синхронизацию и данные.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation(" Инициализация MainPageViewModel");

            _currentChildId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);

            if (string.IsNullOrEmpty(_currentChildId))
            {
                _logger.LogInformation("Текущий ребёнок не выбран — показываем блок-заглушку");
                ShowNoChildSelected = true;
                return;
            }

            ShowNoChildSelected = false;

            await _syncService.InitializeAsync();
            await OnLoadDataAsync();
            await UpdateSyncStatusAsync();

            _logger.LogInformation(" Инициализация завершена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при инициализации MainPageViewModel");
        }
    }

    /// <summary>
    /// Запускает обновление статуса синхронизации (заменяет старый timer polling).
    /// Вызывается из MainPage.OnAppearing.
    /// </summary>
    public void StartStatusTimer()
    {
        // Статус обновляется через Connectivity.Current.ConnectivityChanged.
        // Выполняем начальную проверку сразу.
        _ = UpdateSyncStatusAsync();
    }

    /// <summary>
    /// Больше не останавливает таймер (используем события Connectivity API).
    /// Вызывается из MainPage.OnDisappearing — метод сохранён для обратной совместимости.
    /// </summary>
    public void StopStatusTimer()
    {
        // Connectivity события обрабатываются глобально — не требуют остановки.
    }

    // ========== ОТПРАВКА ИЗМЕРЕНИЯ ==========

    private async Task OnSubmitMeasurementAsync()
    {
        try
        {
            HideError();
            IsLoading = true;

            // Валидация введённого значения
            if (!DoubleParser.TryParseDecrypted(NewGlucoseValue, out double glucoseValue))
            {
                ShowError("Введите корректное число");
                _logger.LogWarning("Некорректное значение глюкозы: {Value}", NewGlucoseValue);
                return;
            }

            if (!GlucoseLevels.IsValidInput(glucoseValue))
            {
                ShowError(
                    $"Введите значение от {GlucoseLevels.InputMinValue:0.0} " +
                    $"до {GlucoseLevels.InputMaxValue:0.0} ммоль/л");
                _logger.LogWarning("Значение глюкозы вне допустимого диапазона: {Value}", glucoseValue);
                return;
            }

            if (string.IsNullOrEmpty(_currentChildId))
            {
                ShowError("Не выбран ребёнок. Выберите ребёнка в профиле.");
                return;
            }

            var childStateStr = SelectedChildState.ToString().ToLowerInvariant();

            var recommendation = await _measurementService.ProcessMeasurementWithRecommendationAsync(
                _currentChildId,
                glucoseValue,
                childStateStr);

            if (recommendation == null)
            {
                ShowError("Ошибка при обработке измерения");
                _logger.LogError("Рекомендация не получена для ChildId={ChildId}", _currentChildId);
                return;
            }

            CurrentRecommendation = recommendation;
            ApplyInlineRecommendation(recommendation);
            await OpenRecommendationModalAsync(recommendation);

            NewGlucoseValue = string.Empty;
            await OnLoadDataAsync();

            _logger.LogInformation(" Измерение обработано успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при отправке измерения");
            ShowError($"Ошибка: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ========== ЗАКРЫТИЕ МОДАЛЬНОГО ОКНА ==========

    private async Task OnCloseModalAsync()
    {
        try
        {
            _logger.LogInformation(" Закрытие модального окна рекомендации");
            IsRecommendationModalOpen = false;
            CurrentRecommendation = null;
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при закрытии модального окна");
        }
    }

    // ========== НАВИГАЦИЯ ==========

    private async Task GoToProfileAsync()
    {
        try
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("//profilepage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка перехода в профиль");
        }
    }

    private async Task NavigateToHelpAsync()
    {
        try
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("helpalertpage");
                return;
            }

            await ShowHelpFallbackAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка перехода на страницу помощи");
            await ShowHelpFallbackAsync();
        }
    }

    private async Task NavigateToChartAsync()
    {
        try
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("chartpage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка перехода на страницу графика");
        }
    }

    private async Task NavigateToScheduleAsync()
    {
        try
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("schedulepage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка перехода на страницу расписания");
        }
    }

    private async Task NavigateToBackpackAsync()
    {
        try
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("//backpackpage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка перехода в рюкзак");
        }
    }

    // ========== ЗАГРУЗКА ДАННЫХ ==========

    private async Task OnLoadDataAsync()
    {
        if (string.IsNullOrEmpty(_currentChildId))
            return;

        try
        {
            _logger.LogInformation(" Загрузка данных главного экрана для ChildId={ChildId}", _currentChildId);
            IsLoading = true;

            // --- Последние два измерения для тренда и мини-графика ---
            await UpdateChildHeaderAsync();

            // --- Последние два измерения для тренда и мини-графика ---
            var measurements = await _measurementService.GetLastTwoMeasurementsAsync(_currentChildId);

            Measurements.Clear();
            foreach (var m in measurements.OrderBy(x => x.MeasurementTime))
                Measurements.Add(m);

            var lastMeasurement = measurements.FirstOrDefault();

            if (lastMeasurement != null)
            {
                // Дешифруем значение глюкозы
                double glucoseValue = 0;
                try
                {
                    var decryptedValue = await _cryptoService.DecryptAsync(lastMeasurement.EncryptedGlucoseValue);
                    if (!DoubleParser.TryParseDecrypted(decryptedValue, out glucoseValue))
                    {
                        _logger.LogWarning("Не удалось распарсить дешифрованное значение глюкозы");
                        DoubleParser.TryParseDecrypted(lastMeasurement.EncryptedGlucoseValue, out glucoseValue);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось дешифровать значение глюкозы");
                    DoubleParser.TryParseDecrypted(lastMeasurement.EncryptedGlucoseValue, out glucoseValue);
                }

                LastGlucoseValue = glucoseValue.ToString("F1", CultureInfo.InvariantCulture);
                _ = PersistLastGlucoseAsync(glucoseValue);
                GlucoseHeroAccessibilityText = $"Уровень глюкозы: {LastGlucoseValue} миллимоль на литр";

                var timeDiff = DateTime.UtcNow - lastMeasurement.MeasurementTime;
                LastMeasurementTime = FormatTimeDiff(timeDiff);

                // Определяем UI-состояние через существующий сервис
                GlucoseStatus status = _measurementService.GetStatus(glucoseValue);
                IsCritical = status is GlucoseStatus.CriticallyLow or GlucoseStatus.CriticallyHigh;

                CurrentGlucoseUiState = status switch
                {
                    GlucoseStatus.Normal => GlucoseUiState.Normal,
                    GlucoseStatus.Low => GlucoseUiState.Attention,
                    GlucoseStatus.High => GlucoseUiState.Attention,
                    GlucoseStatus.CriticallyLow => GlucoseUiState.Critical,
                    GlucoseStatus.CriticallyHigh => GlucoseUiState.Critical,
                    _ => GlucoseUiState.Normal
                };

                LastGlucoseValueDecimal = (decimal)glucoseValue;

                _logger.LogDebug("Последнее измерение: {Value} ммоль/л, статус: {Status}",
                    glucoseValue, status);
            }
            else
            {
                // Нет измерений — сбрасываем все поля в пустое состояние
                LastGlucoseValue = "—";
                _ = PersistLastGlucoseAsync(0d);
                LastMeasurementTime = "—";
                IsCritical = false;
                CurrentGlucoseUiState = GlucoseUiState.Normal;
                LastGlucoseValueDecimal = 0m;
                GlucoseHeroAccessibilityText = "Нет данных об уровне глюкозы";
            }

            // --- Стрелка тренда по двум последним измерениям ---
            await UpdateTrendArrowAsync();

            // --- Мини-график: собираем ChartDataPoint для GraphicsView ---
            await UpdateMiniChartAsync();

            // --- Быстрые значения строятся по личному целевому диапазону ---
            await UpdateGlucoseShortcutsAsync();

            // --- Суммарные ХЕ рюкзака ---
            await UpdateTotalBreadUnitsAsync();

            // --- Обновляем время и дату на экране ---
            CurrentTime = DateTime.Now.ToString("HH:mm");
            CurrentDate = DateTime.Now.ToString("dd MMMM yyyy");

            // --- Краткая статистика дня (считаем все измерения, а не только last 2) ---
            var todayCount = await _measurementService.GetMeasurementCountTodayAsync(_currentChildId);
            DailyStats = todayCount > 0
                ? $"Сегодня: {todayCount} изм."
                : "Сегодня: нет измерений";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке данных главного экрана");
            ShowError($"Ошибка загрузки: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Сохраняет последнее известное значение глюкозы в storage.
    /// Используется HelpAlertPageViewModel для передачи актуального значения в CriticalAlertRequest.
    /// Fire-and-forget: ошибка записи не критична (это кэш для экстренных алертов).
    /// </summary>
    private async Task PersistLastGlucoseAsync(double value)
    {
        try
        {
            await _storageService.SaveAsync(
                AppConstants.StorageKeyLastGlucoseValue,
                value.ToString("F1", CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось сохранить последнее значение глюкозы в storage");
        }
    }

    // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ЗАГРУЗКИ ==========

    /// <summary>
    /// Вычисляет стрелку тренда по двум последним точкам Measurements.
    /// </summary>
    private async Task UpdateTrendArrowAsync()
    {
        if (Measurements.Count >= 2)
        {
            var ordered = Measurements.OrderBy(x => x.MeasurementTime).ToList();
            var prev = ordered[ordered.Count - 2];
            var curr = ordered[ordered.Count - 1];

            double prevVal = 0, currVal = 0;
            try
            {
                var p = await _cryptoService.DecryptAsync(prev.EncryptedGlucoseValue);
                var c = await _cryptoService.DecryptAsync(curr.EncryptedGlucoseValue);
                DoubleParser.TryParseDecrypted(p, out prevVal);
                DoubleParser.TryParseDecrypted(c, out currVal);
            }
            catch
            {
                // Если дешифровка не удалась — оставляем стрелку горизонтальной
            }

            TrendArrow = currVal > prevVal ? "▲" : currVal < prevVal ? "▼" : "→";
        }
        else
        {
            TrendArrow = "→";
        }
    }

    /// <summary>
    /// Строит список ChartDataPoint из Measurements и передаёт их в MiniChartDrawable.
    /// ShowChart обновляется автоматически через NotifyPropertyChangedFor.
    /// </summary>
    private async Task UpdateMiniChartAsync()
    {
        var rangeEnd = DateTime.UtcNow;
        var rangeStart = rangeEnd.AddHours(-24);
        var ordered = await _measurementRepository.GetByDateRangeAsync(
            _currentChildId!,
            rangeStart,
            rangeEnd,
            page: 1,
            pageSize: 48);

        ordered = ordered
            .OrderBy(x => x.MeasurementTime)
            .ToList();

        if (ordered.Count == 0)
        {
            MiniChartPoints.Clear();
            MiniChartDrawable.DataPoints = [];
            OnPropertyChanged(nameof(ShowChart));
            return;
        }

        // Параллельная расшифровка (M-5)
        var decryptedResults = await Task.WhenAll(
            ordered.Select(async m =>
            {
                try
                {
                    var decrypted = await _cryptoService.DecryptAsync(m.EncryptedGlucoseValue);
                    if (DoubleParser.TryParseDecrypted(decrypted, out var glucose))
                    {
                        var status = _measurementService.GetStatus(glucose);
                        var uiState = status switch
                        {
                            GlucoseStatus.CriticallyLow => GlucoseUiState.Critical,
                            GlucoseStatus.CriticallyHigh => GlucoseUiState.Critical,
                            GlucoseStatus.Low => GlucoseUiState.Attention,
                            GlucoseStatus.High => GlucoseUiState.Attention,
                            _ => GlucoseUiState.Normal
                        };
                        return new ChartDataPoint(m.MeasurementTime, (decimal)glucose, uiState);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось обработать точку мини-графика");
                }
                return null;
            }));

        var points = decryptedResults.Where(p => p is not null).Select(p => p!).ToList();

        MiniChartPoints.Clear();
        foreach (var p in points)
            MiniChartPoints.Add(p);

        MiniChartDrawable.DataPoints = points;
        OnPropertyChanged(nameof(ShowChart));
    }

    /// <summary>
    /// Загружает суммарные ХЕ из рюкзака через репозиторий.
    /// Использует GetDecryptedBreadUnitsAsync — единственный корректный способ
    /// получить расшифрованное значение EncryptedBreadUnits.
    /// </summary>
    private async Task UpdateTotalBreadUnitsAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_currentChildId))
            {
                TotalBreadUnits = 0.0;
                return;
            }

            var items = await _backpackRepository.GetByChildIdAsync(_currentChildId);
            if (items == null || !items.Any())
            {
                TotalBreadUnits = 0.0;
                return;
            }

            var decryptedValues = await Task.WhenAll(items.Select(async item =>
            {
                try
                {
                    return await _backpackRepository.GetDecryptedBreadUnitsAsync(item);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось дешифровать ХЕ для ItemId={ItemId}", item.BackpackItemId);
                    return 0.0;
                }
            }));

            var total = decryptedValues.Sum();

            TotalBreadUnits = total;
            ShowBackpackSummary = total > 0;
            _logger.LogDebug("Суммарные ХЕ рюкзака: {Total}", total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при загрузке ХЕ рюкзака");
            TotalBreadUnits = 0.0;
        }
    }

    private async Task UpdateGlucoseShortcutsAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentChildId))
        {
            return;
        }

        try
        {
            var settings = await _diabetesSettingsRepository.GetByChildIdAsync(_currentChildId);
            if (settings is null)
            {
                return;
            }

            var minTask = _diabetesSettingsRepository.GetDecryptedTargetRangeMinAsync(settings);
            var maxTask = _diabetesSettingsRepository.GetDecryptedTargetRangeMaxAsync(settings);
            await Task.WhenAll(minTask, maxTask);

            var min = Math.Clamp(await minTask, 2.0, 20.0);
            var max = Math.Clamp(await maxTask, min, 25.0);
            var middle = (min + max) / 2.0;

            GlucoseShortcut1 = FormatShortcut(min);
            GlucoseShortcut2 = FormatShortcut(middle);
            GlucoseShortcut3 = FormatShortcut(max);
            GlucoseShortcut4 = FormatShortcut(Math.Min(max + 2.0, 30.0));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось построить быстрые значения глюкозы для ChildId={ChildId}", _currentChildId);
        }
    }

    private static string FormatShortcut(double value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);

    private async Task UpdateChildHeaderAsync()
    {
        try
        {
            var child = await _childRepository.GetByIdAsync(_currentChildId!);
            if (child is null)
            {
                var nickname = await _storageService.GetAsync("child_nickname");
                ChildName = string.IsNullOrWhiteSpace(nickname)
                    ? "друг"
                    : nickname.Trim();
                return;
            }

            var firstName = await _childRepository.GetFirstNameAsync(child);
            ChildName = string.IsNullOrWhiteSpace(firstName)
                ? "друг"
                : firstName.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось загрузить имя ребёнка для главной");
            ChildName = "друг";
        }
    }

    private void ApplyInlineRecommendation(RecommendationResponse recommendation)
    {
        var fallbackText = GlucoseClassifier.Classify(recommendation.GlucoseValueAtRequest) switch
        {
            GlucoseStatus.CriticallyLow =>
                "Глюкоза критически низкая. Немедленно позови взрослого, прими быстрые углеводы по своему плану и повтори измерение через 10-15 минут.",
            GlucoseStatus.Low =>
                "Глюкоза низкая. Позови взрослого, прими быстрые углеводы по своему плану и повтори измерение через 10-15 минут.",
            GlucoseStatus.High =>
                "Глюкоза выше цели. Сообщи взрослому, пей воду и действуй по своему плану коррекции. Не ешь дополнительные углеводы без взрослого.",
            GlucoseStatus.CriticallyHigh =>
                "Глюкоза очень высокая. Сразу сообщи взрослому, пей воду и проверь кетоны по своему плану.",
            _ =>
                "Измерение сохранено. Сахар в целевом диапазоне, продолжай обычный режим."
        };

        RecommendationText = string.IsNullOrWhiteSpace(recommendation.RecommendationText)
            ? fallbackText
            : recommendation.RecommendationText;

        RecommendationUrgency = string.IsNullOrWhiteSpace(recommendation.Urgency)
            ? "Совет готов"
            : recommendation.Urgency;

        var urgency = recommendation.Urgency ?? string.Empty;
        UrgencyKey = urgency.Contains("крит", StringComparison.OrdinalIgnoreCase) ||
                     urgency.Contains("critical", StringComparison.OrdinalIgnoreCase)
            ? "Danger"
            : urgency.Contains("вним", StringComparison.OrdinalIgnoreCase) ||
              urgency.Contains("warning", StringComparison.OrdinalIgnoreCase)
                ? "Warning"
                : "Primary";

        RecommendationUrgencyAccessibilityText = RecommendationUrgency;
        ShowRecommendation = true;
    }

    private static Task ShowHelpFallbackAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page?.DisplayAlert(
            "Помощь",
            "Если сахар низкий: сядь, съешь быстрые углеводы и сообщи взрослому. Если сахар высокий: выпей воды, проверь самочувствие и попроси взрослого помочь. При плохом самочувствии сразу звони родителю.",
            "Понятно") ?? Task.CompletedTask;
    }

    // ========== ОТКРЫТИЕ МОДАЛЬНОГО ОКНА РЕКОМЕНДАЦИИ ==========

    private async Task OpenRecommendationModalAsync(RecommendationResponse recommendation)
    {
        try
        {
            _logger.LogInformation("Открытие модального окна рекомендации");

            var modalViewModel = _recommendationModalViewModelFactory.Create();
            await modalViewModel.SetRecommendationAsync(recommendation);

            var modal = _recommendationModalFactory.Create(modalViewModel);
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page?.Navigation is not null)
            {
                await page.Navigation.PushModalAsync(modal);
            }

            _logger.LogInformation("Модальное окно рекомендации открыто");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при открытии модального окна рекомендации");
        }
    }

    // ========== ОШИБКИ ==========

    private void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorVisible = true;
        _logger.LogWarning("Ошибка на главном экране: {Message}", message);
    }

    private void HideError()
    {
        ErrorMessage = string.Empty;
        IsErrorVisible = false;
    }

    // ========== СТАТУС СИНХРОНИЗАЦИИ ==========

    /// <summary>
    /// Обновляет отображаемое состояние синхронизации после операций с данными
    /// и при изменении сетевого подключения.
    /// </summary>
    private async Task UpdateSyncStatusAsync()
    {
        try
        {
            var status = await _syncService.GetStatusAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsOffline = !status.IsConnected;
                PendingSyncCount = status.PendingItemsCount;

                if (IsOffline)
                {
                    OfflineStatusText = PendingSyncCount > 0
                        ? $"Офлайн • {PendingSyncCount} в очереди"
                        : "Офлайн";
                    ShowOfflineIndicator = true;
                    SyncStatus = OfflineStatusText;
                }
                else
                {
                    if (PendingSyncCount > 0)
                    {
                        OfflineStatusText = $"Синхронизация • {PendingSyncCount} осталось";
                        ShowOfflineIndicator = true;
                        SyncStatus = OfflineStatusText;
                    }
                    else
                    {
                        ShowOfflineIndicator = false;
                        SyncStatus = "Синхронизировано";
                        OfflineStatusText = string.Empty;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при обновлении статуса синхронизации");
        }
    }

    // ========== ОБРАБОТЧИКИ СОБЫТИЙ ==========

    /// <summary>
    /// Обработчик события изменения сетевого подключения от MAUI Connectivity API.
    /// Заменяет старый timer-based polling (C-3).
    /// </summary>
    private async void OnPlatformConnectivityChanged(object? sender, Microsoft.Maui.Networking.ConnectivityChangedEventArgs e)
    {
        try
        {
            _logger.LogInformation(" Сетевое подключение изменилось: {Status}", e.NetworkAccess);
            await UpdateSyncStatusAsync();

            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                _logger.LogInformation(" Сеть появилась — запуск синхронизации");
                _ = _syncService.SyncNowAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка в OnPlatformConnectivityChanged");
        }
    }

    private async void OnConnectivityChanged(
        object? sender,
        Services.Interfaces.ConnectivityChangedEventArgs e)
    {
        try
        {
            _logger.LogInformation(" Статус соединения изменился: {IsConnected}", e.IsConnected);
            await UpdateSyncStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка в OnConnectivityChanged");
        }
    }

    private async void OnSyncStarted(
        object? sender,
        Services.Interfaces.SyncStartedEventArgs e)
    {
        try
        {
            _logger.LogInformation(" Синхронизация начата: {ItemsCount} элементов", e.ItemsCount);
            await UpdateSyncStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка в OnSyncStarted");
        }
    }

    private async void OnSyncCompleted(
        object? sender,
        Services.Interfaces.SyncCompletedEventArgs e)
    {
        try
        {
            _logger.LogInformation(
                " Синхронизация завершена: {SuccessCount} успешно, {ErrorCount} ошибок",
                e.SuccessCount, e.ErrorCount);
            await UpdateSyncStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка в OnSyncCompleted");
        }
    }

    // ========== ВСПОМОГАТЕЛЬНЫЙ МЕТОД ФОРМАТИРОВАНИЯ ВРЕМЕНИ ==========

    /// <summary>
    /// Возвращает человекочитаемое время с момента последнего измерения.
    /// Примеры: "только что", "5м назад", "2ч назад", "3д назад".
    /// </summary>
    private static string FormatTimeDiff(TimeSpan diff)
    {
        if (diff.TotalSeconds < 60) return "только что";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}м назад";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}ч назад";
        return $"{(int)diff.TotalDays}д назад";
    }

    // ========== DISPOSE ==========

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Таймер null (заменён на Connectivity API) — пропускаем
        Connectivity.Current.ConnectivityChanged -= OnPlatformConnectivityChanged;

        if (_syncService != null)
        {
            _syncService.ConnectivityChanged -= OnConnectivityChanged;
            _syncService.SyncStarted -= OnSyncStarted;
            _syncService.SyncCompleted -= OnSyncCompleted;
        }
    }
}
