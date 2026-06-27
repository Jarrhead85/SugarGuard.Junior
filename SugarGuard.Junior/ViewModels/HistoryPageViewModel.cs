using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Shared.Constants;
using SugarGuard.Domain.Enums;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Views.Components;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// Диапазон дат для фильтрации истории.
/// </summary>
public readonly record struct DateRange(DateTime From, DateTime To);

/// <summary>
/// Варианты фильтра по дате для экрана истории.
/// </summary>
public enum DateFilterOption
{
    Today,
    Last7Days,
    Last30Days,
    Custom
}

/// <summary>
/// Presentation-модель пустого состояния.
/// </summary>
public sealed record HistoryEmptyStatePresentation(
    string Icon,
    string Title,
    string Message);

/// <summary>
/// Presentation-модель одного измерения для экрана истории.
/// </summary>
public sealed class MeasurementDisplayItem
{
    /// <summary>
    /// Идентификатор измерения.
    /// </summary>
    public Guid MeasurementId { get; init; }

    /// <summary>
    /// Значение глюкозы в ммоль/л.
    /// </summary>
    public decimal GlucoseValue { get; init; }

    /// <summary>
    /// Время измерения.
    /// </summary>
    public DateTime MeasurementTime { get; init; }

    /// <summary>
    /// UI-состояние значения глюкозы.
    /// </summary>
    public GlucoseUiState UiState { get; init; }

    /// <summary>
    /// Заметка к измерению.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Флаг синхронизации измерения.
    /// </summary>
    public bool IsSynced { get; init; }

    /// <summary>
    /// Текстовый источник данных.
    /// </summary>
    public string? SourceLabel { get; init; }

    /// <summary>
    /// Значение для крупного display-числа.
    /// </summary>
    public string DisplayValue => GlucoseValue.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Единица измерения для UI.
    /// </summary>
    public string DisplayUnit => "ммоль/л";

    /// <summary>
    /// Полная дата и время для одной строки.
    /// </summary>
    public string DisplayDateTime => UiMeasurementTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Дата отдельным полем.
    /// Удобно для более чистых карточек.
    /// </summary>
    public string DisplayDate => UiMeasurementTime.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Время отдельным полем.
    /// </summary>
    public string DisplayTime => UiMeasurementTime.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Человеко-понятная подпись дня.
    /// </summary>
    public string DayLabel
    {
        get
        {
            var date = UiMeasurementTime.Date;
            var today = DateTime.Now.Date;

            if (date == today)
            {
                return "Сегодня";
            }

            if (date == today.AddDays(-1))
            {
                return "Вчера";
            }

            return UiMeasurementTime.ToString("dd MMMM", new CultureInfo("ru-RU"));
        }
    }

    /// <summary>
    /// Есть ли заметка.
    /// </summary>
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    /// <summary>
    /// Подготовленная заметка для короткого текста в карточке.
    /// </summary>
    public string NotesPreview => HasNotes
        ? NormalizeWhitespace(Notes!)
        : string.Empty;

    /// <summary>
    /// Короткий текст статуса для secondary-подписи.
    /// </summary>
    public string StatusText => UiState switch
    {
        GlucoseUiState.Normal => "В норме",
        GlucoseUiState.Attention => "Требует внимания",
        GlucoseUiState.Critical => "Критично",
        _ => "Неизвестно"
    };

    /// <summary>
    /// Расширенное описание статуса.
    /// </summary>
    public string StatusDescription => UiState switch
    {
        GlucoseUiState.Normal => "Показатель находится в целевом диапазоне.",
        GlucoseUiState.Attention => "Показатель выходит за целевой диапазон и требует внимания.",
        GlucoseUiState.Critical => "Показатель находится в критической зоне.",
        _ => "Статус показателя не определён."
    };

    /// <summary>
    /// Короткая подпись синхронизации.
    /// </summary>
    public string SyncText => IsSynced ? "Синхронизировано" : "Ожидает синхронизации";

    /// <summary>
    /// Готовая строка для screen reader / automation.
    /// </summary>
    public string AccessibilityText =>
        $"{DisplayValue} {DisplayUnit}, {StatusText}, {DisplayDateTime}"
        + (HasNotes ? $", заметка: {NotesPreview}" : string.Empty);

    /// <summary>
    /// Время, приведённое к UI-представлению.
    /// </summary>
    private DateTime UiMeasurementTime =>
        MeasurementTime.Kind == DateTimeKind.Utc
            ? MeasurementTime.ToLocalTime()
            : MeasurementTime;

    /// <summary>
    /// Нормализует пробелы в заметке для компактного отображения.
    /// </summary>
    private static string NormalizeWhitespace(string value)
    {
        return string.Join(
            " ",
            value.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>
/// ViewModel страницы истории измерений.
/// </summary>
public partial class HistoryPageViewModel : ObservableObject
{
    private readonly IMeasurementRepository _measurementRepository;
    private readonly IStorageService _storageService;
    private readonly ICryptoService _cryptoService;
    private readonly ILogger<HistoryPageViewModel> _logger;

    private string? _currentChildId;
    private int _currentPage;
    private const int PageSize = 5;
    private bool _isNormalizingCustomRange;

    /// <summary>
    /// Идёт загрузка следующей страницы (infinite scroll).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusyStateVisible))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    [NotifyPropertyChangedFor(nameof(IsEmptyStateVisible))]
    private bool isLoadingMore;

    /// <summary>
    /// Основной список измерений для CollectionView.
    /// </summary>
    public ObservableCollection<MeasurementDisplayItem> Measurements { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasMeasurements))]
    [NotifyPropertyChangedFor(nameof(IsTodaySelected))]
    [NotifyPropertyChangedFor(nameof(IsLast7DaysSelected))]
    [NotifyPropertyChangedFor(nameof(IsLast30DaysSelected))]
    [NotifyPropertyChangedFor(nameof(IsCustomSelected))]
    [NotifyPropertyChangedFor(nameof(ShouldShowCustomRange))]
    [NotifyPropertyChangedFor(nameof(SelectedFilterTitle))]
    [NotifyPropertyChangedFor(nameof(SelectedPeriodLabel))]
    [NotifyPropertyChangedFor(nameof(ResultsSummaryText))]
    [NotifyPropertyChangedFor(nameof(EmptyStateIcon))]
    [NotifyPropertyChangedFor(nameof(EmptyStateTitle))]
    [NotifyPropertyChangedFor(nameof(EmptyStateMessage))]
    [NotifyPropertyChangedFor(nameof(EmptyState))]
    private DateFilterOption selectedFilter = DateFilterOption.Last7Days;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusyStateVisible))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    [NotifyPropertyChangedFor(nameof(IsEmptyStateVisible))]
    [NotifyPropertyChangedFor(nameof(ResultsSummaryText))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNextPage))]
    private bool hasMorePages = true;

    public bool CanGoPreviousPage => _currentPage > 0;

    public bool CanGoNextPage => HasMorePages;

    public string PageLabel => $"Страница {_currentPage + 1}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    [NotifyPropertyChangedFor(nameof(IsEmptyStateVisible))]
    [NotifyPropertyChangedFor(nameof(SelectedPeriodLabel))]
    [NotifyPropertyChangedFor(nameof(EmptyStateMessage))]
    [NotifyPropertyChangedFor(nameof(EmptyState))]
    private DateTime customFrom = DateTime.Today.AddDays(-7);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPeriodLabel))]
    [NotifyPropertyChangedFor(nameof(EmptyStateMessage))]
    [NotifyPropertyChangedFor(nameof(EmptyState))]
    private DateTime customTo = DateTime.Today;

    public HistoryPageViewModel(
        IMeasurementRepository measurementRepository,
        IStorageService storageService,
        ICryptoService cryptoService,
        ILogger<HistoryPageViewModel> logger)
    {
        _measurementRepository = measurementRepository;
        _storageService = storageService;
        _cryptoService = cryptoService;
        _logger = logger;

        Measurements.CollectionChanged += OnMeasurementsCollectionChanged;
    }

    /// <summary>
    /// История пуста.
    /// </summary>
    public bool IsEmpty => Measurements.Count == 0;

    /// <summary>
    /// История содержит данные.
    /// </summary>
    public bool HasMeasurements => Measurements.Count > 0;

    public bool HasChart => Measurements.Count >= 2;

    public string AverageGlucose => Measurements.Count == 0
        ? "—"
        : Measurements.Average(item => item.GlucoseValue).ToString("0.0", CultureInfo.InvariantCulture);

    public GlucoseChartDrawable ChartDrawable { get; } = new();

    /// <summary>
    /// Видимость busy-состояния.
    /// </summary>
    public bool IsBusyStateVisible => IsLoading || IsLoadingMore;

    /// <summary>
    /// Видимость списка.
    /// </summary>
    public bool IsListVisible => !IsLoading && !IsLoadingMore && HasMeasurements;

    /// <summary>
    /// Видимость empty-state.
    /// </summary>
    public bool IsEmptyStateVisible => !IsLoading && !IsLoadingMore && IsEmpty;

    /// <summary>
    /// Выбран ли фильтр "Сегодня".
    /// </summary>
    public bool IsTodaySelected => SelectedFilter == DateFilterOption.Today;

    /// <summary>
    /// Выбран ли фильтр "7 дней".
    /// </summary>
    public bool IsLast7DaysSelected => SelectedFilter == DateFilterOption.Last7Days;

    /// <summary>
    /// Выбран ли фильтр "30 дней".
    /// </summary>
    public bool IsLast30DaysSelected => SelectedFilter == DateFilterOption.Last30Days;

    /// <summary>
    /// Выбран ли пользовательский диапазон.
    /// </summary>
    public bool IsCustomSelected => SelectedFilter == DateFilterOption.Custom;

    /// <summary>
    /// Нужно ли показывать блок выбора произвольного периода.
    /// </summary>
    public bool ShouldShowCustomRange => SelectedFilter == DateFilterOption.Custom;

    /// <summary>
    /// Человекочитаемое название текущего фильтра.
    /// </summary>
    public string SelectedFilterTitle => SelectedFilter switch
    {
        DateFilterOption.Today => "За сегодня",
        DateFilterOption.Last7Days => "За 7 дней",
        DateFilterOption.Last30Days => "За 30 дней",
        DateFilterOption.Custom => "За выбранный период",
        _ => "История"
    };

    /// <summary>
    /// Подпись периода для UI.
    /// </summary>
    public string SelectedPeriodLabel => SelectedFilter switch
    {
        DateFilterOption.Today => "Сегодня",
        DateFilterOption.Last7Days => "Последние 7 дней",
        DateFilterOption.Last30Days => "Последние 30 дней",
        DateFilterOption.Custom => $"{CustomFrom:dd.MM.yyyy} — {CustomTo:dd.MM.yyyy}",
        _ => string.Empty
    };

    /// <summary>
    /// Подпись с количеством результатов.
    /// </summary>
    public string ResultsSummaryText
    {
        get
        {
            if (IsLoading)
            {
                return "Загружаем историю измерений…";
            }

            if (IsEmpty)
            {
                return "Записей не найдено";
            }

            return $"{Measurements.Count} {GetMeasurementsWordForm(Measurements.Count)} · {SelectedPeriodLabel}";
        }
    }

    /// <summary>
    /// Иконка для пустого состояния.
    /// </summary>
    public string EmptyStateIcon => string.IsNullOrWhiteSpace(_currentChildId)
        ? "�"
        : SelectedFilter == DateFilterOption.Today
            ? "�"
            : "�";

    /// <summary>
    /// Заголовок пустого состояния.
    /// </summary>
    public string EmptyStateTitle
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_currentChildId))
            {
                return "Ребёнок не выбран";
            }

            return SelectedFilter switch
            {
                DateFilterOption.Today => "Сегодня пока нет измерений",
                DateFilterOption.Last7Days => "За последние 7 дней измерений нет",
                DateFilterOption.Last30Days => "За последние 30 дней измерений нет",
                DateFilterOption.Custom => "За выбранный период ничего не найдено",
                _ => "История пока пуста"
            };
        }
    }

    /// <summary>
    /// Пояснение для пустого состояния.
    /// </summary>
    public string EmptyStateMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_currentChildId))
            {
                return "Сначала выбери профиль ребёнка, чтобы открыть историю измерений.";
            }

            return SelectedFilter switch
            {
                DateFilterOption.Today => "Добавь первое измерение за сегодня, и оно появится здесь.",
                DateFilterOption.Last7Days => "Попробуй расширить период или проверь, были ли измерения сохранены локально.",
                DateFilterOption.Last30Days => "История за месяц пуста. Возможно, стоит выбрать другой период.",
                DateFilterOption.Custom => $"В диапазоне {CustomFrom:dd.MM.yyyy} — {CustomTo:dd.MM.yyyy} измерения не найдены.",
                _ => "Когда появятся измерения, они будут показаны в этом разделе."
            };
        }
    }

    /// <summary>
    /// Полная presentation-модель пустого состояния.
    /// </summary>
    public HistoryEmptyStatePresentation EmptyState =>
        new(EmptyStateIcon, EmptyStateTitle, EmptyStateMessage);

    /// <summary>
    /// Инициализация ViewModel.
    /// </summary>
    public async Task InitializeAsync()
    {
        _currentChildId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);
        await ReloadCurrentFilterAsync();
    }

    /// <summary>
    /// Загружает измерения за выбранный диапазон (сбрасывает пагинацию).
    /// </summary>
    public async Task LoadMeasurementsAsync(DateRange range)
    {
        _currentPage = 0;
        HasMorePages = true;
        Measurements.Clear();
        await LoadMeasurementsPageAsync(range, _currentPage, replace: true);
    }

    /// <summary>
    /// Загружает следующую страницу измерений (infinite scroll).
    /// </summary>
    [RelayCommand]
    private async Task LoadNextPageAsync()
    {
        if (IsLoadingMore || !HasMorePages) return;

        IsLoadingMore = true;
        try
        {
            _currentPage++;
            await LoadMeasurementsPageAsync(GetDateRangeForFilter(SelectedFilter), _currentPage, replace: true);
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private async Task LoadPreviousPageAsync()
    {
        if (IsLoadingMore || _currentPage <= 0)
            return;

        IsLoadingMore = true;
        try
        {
            _currentPage--;
            await LoadMeasurementsPageAsync(GetDateRangeForFilter(SelectedFilter), _currentPage, replace: true);
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    /// <summary>
    /// Загружает одну страницу измерений.
    /// </summary>
    private async Task LoadMeasurementsPageAsync(DateRange range, int page, bool replace)
    {
        if (string.IsNullOrWhiteSpace(_currentChildId))
        {
            _logger.LogInformation("Текущий ребёнок не выбран — история не загружена.");
            ReplaceMeasurements(Array.Empty<MeasurementDisplayItem>());
            RefreshPresentationState();
            return;
        }

        try
        {
            if (replace)
                IsLoading = true;

            var rawMeasurements = await _measurementRepository.GetByDateRangeAsync(
                _currentChildId,
                range.From,
                range.To,
                page: page + 1,
                pageSize: PageSize);

            HasMorePages = rawMeasurements.Count == PageSize;

            // HIGH-9: Параллельная декрипция через Task.WhenAll.
            // Раньше: для PageSize=30 — 60 последовательных await (2 на каждое измерение).
            // Теперь: одна параллельная пачка; порядок результатов сохраняется.
            var itemTasks = new Task<MeasurementDisplayItem>[rawMeasurements.Count];
            for (var i = 0; i < rawMeasurements.Count; i++)
            {
                itemTasks[i] = BuildMeasurementItemAsync(rawMeasurements[i]);
            }

            var items = (await Task.WhenAll(itemTasks)).ToList();

            if (replace)
                ReplaceMeasurements(items);
            else
                AppendMeasurements(items);

            OnPropertyChanged(nameof(CanGoPreviousPage));
            OnPropertyChanged(nameof(CanGoNextPage));
            OnPropertyChanged(nameof(PageLabel));

            _logger.LogInformation(
                "Загружено {Count} измерений за период {From} — {To} (страница {Page}).",
                items.Count,
                range.From,
                range.To,
                page);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке истории измерений.");
            if (replace)
                ReplaceMeasurements(Array.Empty<MeasurementDisplayItem>());
        }
        finally
        {
            if (replace)
                IsLoading = false;
            RefreshPresentationState();
        }
    }

    /// <summary>
    /// Команда выбора фильтра "Сегодня".
    /// </summary>
    [RelayCommand]
    private async Task SelectTodayAsync()
    {
        await ApplyFilterAsync(DateFilterOption.Today);
    }

    /// <summary>
    /// Команда выбора фильтра "7 дней".
    /// </summary>
    [RelayCommand]
    private async Task SelectLast7DaysAsync()
    {
        await ApplyFilterAsync(DateFilterOption.Last7Days);
    }

    /// <summary>
    /// Команда выбора фильтра "30 дней".
    /// </summary>
    [RelayCommand]
    private async Task SelectLast30DaysAsync()
    {
        await ApplyFilterAsync(DateFilterOption.Last30Days);
    }

    /// <summary>
    /// Команда выбора произвольного диапазона.
    /// </summary>
    [RelayCommand]
    private async Task SelectCustomRangeAsync()
    {
        NormalizeCustomRangeIfNeeded();
        await ApplyFilterAsync(DateFilterOption.Custom);
    }

    /// <summary>
    /// Команда обновления текущего фильтра.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ReloadCurrentFilterAsync();
    }

    /// <summary>
    /// Реакция на смену выбранного фильтра.
    /// </summary>
    partial void OnSelectedFilterChanged(DateFilterOption value)
    {
        RefreshPresentationState();
    }

    /// <summary>
    /// Реакция на изменение начала пользовательского диапазона.
    /// </summary>
    partial void OnCustomFromChanged(DateTime value)
    {
        NormalizeCustomRangeIfNeeded();
        RefreshPresentationState();
    }

    /// <summary>
    /// Реакция на изменение конца пользовательского диапазона.
    /// </summary>
    partial void OnCustomToChanged(DateTime value)
    {
        NormalizeCustomRangeIfNeeded();
        RefreshPresentationState();
    }

    /// <summary>
    /// Применяет выбранный фильтр и загружает данные.
    /// </summary>
    private async Task ApplyFilterAsync(DateFilterOption filter)
    {
        SelectedFilter = filter;
        await LoadMeasurementsAsync(GetDateRangeForFilter(filter));
    }

    /// <summary>
    /// Перезагружает данные для уже выбранного фильтра.
    /// </summary>
    private async Task ReloadCurrentFilterAsync()
    {
        await LoadMeasurementsAsync(GetDateRangeForFilter(SelectedFilter));
    }

    /// <summary>
    /// Возвращает диапазон дат для выбранного фильтра.
    /// </summary>
    private DateRange GetDateRangeForFilter(DateFilterOption filter)
    {
        var now = DateTime.Now;

        return filter switch
        {
            DateFilterOption.Today => new DateRange(now.Date, now),
            DateFilterOption.Last7Days => new DateRange(now.Date.AddDays(-6), now),
            DateFilterOption.Last30Days => new DateRange(now.Date.AddDays(-29), now),
            DateFilterOption.Custom => new DateRange(
                CustomFrom.Date,
                CustomTo.Date.AddDays(1).AddTicks(-1)),
            _ => new DateRange(now.Date, now)
        };
    }

    /// <summary>
    /// Нормализует пользовательский диапазон дат.
    /// </summary>
    private void NormalizeCustomRangeIfNeeded()
    {
        if (_isNormalizingCustomRange)
        {
            return;
        }

        if (CustomFrom <= CustomTo)
        {
            return;
        }

        try
        {
            _isNormalizingCustomRange = true;

            var from = CustomTo;
            var to = CustomFrom;

            CustomFrom = from;
            CustomTo = to;
        }
        finally
        {
            _isNormalizingCustomRange = false;
        }
    }

    /// <summary>
    /// Параллельно расшифровывает глюкозу и заметку измерения, собирает display-модель.
    /// <para>
    /// HIGH-9: оба decrypt-вызова запускаются конкурентно через <see cref="Task.WhenAll(Task[])"/> —
    /// раньше они выполнялись последовательно (2 await на каждое измерение × PageSize = 60 await).
    /// </para>
    /// </summary>
    private async Task<MeasurementDisplayItem> BuildMeasurementItemAsync(
        SugarGuard.Junior.Models.Core.Measurement measurement)
    {
        // Запускаем оба decrypt-вызова параллельно
        var glucoseTask = ResolveGlucoseValueAsync(
            measurement.EncryptedGlucoseValue,
            measurement.MeasurementId);

        var notesTask = ResolveNotesAsync(
            measurement.EncryptedNotes,
            measurement.MeasurementId);

        await Task.WhenAll(glucoseTask, notesTask);

        var glucoseValue = await glucoseTask;
        var notes = await notesTask;

        return new MeasurementDisplayItem
        {
            MeasurementId = ParseMeasurementId(measurement.MeasurementId),
            GlucoseValue = Convert.ToDecimal(glucoseValue),
            MeasurementTime = measurement.MeasurementTime,
            UiState = ResolveUiState(glucoseValue),
            Notes = notes,
            IsSynced = measurement.IsSynced,
            SourceLabel = measurement.DataSource.ToString()
        };
    }

    /// <summary>
    /// Дешифрует и парсит значение глюкозы.
    /// </summary>
    private async Task<double> ResolveGlucoseValueAsync(string encryptedValue, string measurementId)
    {
        try
        {
            var decrypted = await _cryptoService.DecryptAsync(encryptedValue);

            if (TryParseGlucoseValue(decrypted, out var glucose))
            {
                return glucose;
            }

            _logger.LogWarning(
                "После расшифровки не удалось распарсить значение глюкозы для измерения {MeasurementId}.",
                measurementId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не удалось расшифровать значение глюкозы для измерения {MeasurementId}. Пробуем fallback.",
                measurementId);
        }

        if (TryParseGlucoseValue(encryptedValue, out var fallbackValue))
        {
            return fallbackValue;
        }

        _logger.LogWarning(
            "Не удалось получить значение глюкозы даже через fallback для измерения {MeasurementId}. Возвращаем 0.",
            measurementId);

        return 0d;
    }

    /// <summary>
    /// Дешифрует заметку.
    /// </summary>
    private async Task<string?> ResolveNotesAsync(string? encryptedNotes, string measurementId)
    {
        if (string.IsNullOrWhiteSpace(encryptedNotes))
        {
            return null;
        }

        try
        {
            var decrypted = await _cryptoService.DecryptAsync(encryptedNotes);

            if (string.IsNullOrWhiteSpace(decrypted))
            {
                return null;
            }

            return NormalizeTextForUi(decrypted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не удалось расшифровать заметку для измерения {MeasurementId}.",
                measurementId);

            return null;
        }
    }

    /// <summary>
    /// Парсинг значения глюкозы с поддержкой разных культур.
    /// </summary>
    private static bool TryParseGlucoseValue(string rawValue, out double value)
    {
        value = 0d;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        return DoubleParser.TryParseDecrypted(rawValue, out value);
    }

    /// <summary>
    /// Убирает лишние переводы строк и повторяющиеся пробелы,
    /// </summary>
    private static string NormalizeTextForUi(string value)
    {
        return string.Join(
            " ",
            value.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Обновляет коллекцию измерений без изменения публичного контракта страницы.
    /// </summary>
    private void ReplaceMeasurements(IEnumerable<MeasurementDisplayItem> items)
    {
        Measurements.Clear();

        foreach (var item in items)
        {
            Measurements.Add(item);
        }
    }

    /// <summary>
    /// Добавляет измерения в конец коллекции (для подгрузки страниц).
    /// </summary>
    private void AppendMeasurements(IEnumerable<MeasurementDisplayItem> items)
    {
        foreach (var item in items)
        {
            Measurements.Add(item);
        }
    }

    /// <summary>
    /// Обновляет вычисляемые presentation-свойства.
    /// </summary>
    private void RefreshPresentationState()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasMeasurements));
        OnPropertyChanged(nameof(IsBusyStateVisible));
        OnPropertyChanged(nameof(IsListVisible));
        OnPropertyChanged(nameof(IsEmptyStateVisible));
        OnPropertyChanged(nameof(SelectedFilterTitle));
        OnPropertyChanged(nameof(SelectedPeriodLabel));
        OnPropertyChanged(nameof(ResultsSummaryText));
        OnPropertyChanged(nameof(EmptyStateIcon));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateMessage));
        OnPropertyChanged(nameof(EmptyState));
    }

    /// <summary>
    /// Слушатель изменений коллекции.
    /// </summary>
    private void OnMeasurementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ChartDrawable.DataPoints = Measurements
            .OrderBy(item => item.MeasurementTime)
            .Select(item => new ChartDataPoint(item.MeasurementTime, item.GlucoseValue, item.UiState))
            .ToList();
        OnPropertyChanged(nameof(HasChart));
        OnPropertyChanged(nameof(AverageGlucose));
        RefreshPresentationState();
    }

    /// <summary>
    /// Преобразует строковый идентификатор измерения в Guid.
    /// </summary>
    private static Guid ParseMeasurementId(string? measurementId)
    {
        return Guid.TryParse(measurementId, out var guid)
            ? guid
            : Guid.Empty;
    }

    /// <summary>
    /// Определяет UI-состояние сахара по бизнес-порогам.
    /// </summary>
    private static GlucoseUiState ResolveUiState(double glucose)
    {
        if (GlucoseLevels.IsCritical(glucose))
        {
            return GlucoseUiState.Critical;
        }

        if (glucose >= GlucoseLevels.TargetRangeMin && glucose <= GlucoseLevels.TargetRangeMax)
        {
            return GlucoseUiState.Normal;
        }

        return GlucoseUiState.Attention;
    }

    /// <summary>
    /// Склонение слова "измерение" для summary-подписей.
    /// </summary>
    private static string GetMeasurementsWordForm(int count)
    {
        var lastTwoDigits = count % 100;
        var lastDigit = count % 10;

        if (lastTwoDigits is >= 11 and <= 14)
        {
            return "измерений";
        }

        return lastDigit switch
        {
            1 => "измерение",
            2 or 3 or 4 => "измерения",
            _ => "измерений"
        };
    }
}
