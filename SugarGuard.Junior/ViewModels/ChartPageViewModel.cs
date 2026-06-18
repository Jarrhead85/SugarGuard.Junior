using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microcharts;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;
using SugarGuard.Junior.Views.Components;
using SkiaSharp;
using System.Collections.ObjectModel;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// ViewModel страницы графика
/// </summary>
public partial class ChartPageViewModel : ObservableObject
{
    private readonly IMeasurementService _measurementService;
    private readonly IMeasurementRepository _measurementRepository;
    private readonly IStatisticsService _statisticsService;
    private readonly IApiClient _apiClient;
    private readonly ILogger<ChartPageViewModel> _logger;
    private readonly IStorageService _storageService;

    //OBSERVABLE PROPERTIES

    [ObservableProperty]
    private bool isTodaySelected = true;

    [ObservableProperty]
    private bool isWeekSelected = false;

    [ObservableProperty]
    private bool isMonthSelected = false;

    [ObservableProperty]
    private bool isLoading = true;

    // Статистика (нули до загрузки, чтобы не показывать фейковые данные) 
    [ObservableProperty]
    private double timeInRangePercent = 0;

    [ObservableProperty]
    private string timeInRangeText = "—";

    [ObservableProperty]
    private double timeInRangeWidth = 0;

    [ObservableProperty]
    private double averageGlucose = 0;

    [ObservableProperty]
    private double minGlucose = 0;

    [ObservableProperty]
    private double maxGlucose = 0;

    [ObservableProperty]
    private string minMaxGlucose = "— / —";

    [ObservableProperty]
    private int measurementCount = 0;

    [ObservableProperty]
    private double standardDeviation = 0;

    [ObservableProperty]
    private int hypoEpisodes = 0;

    [ObservableProperty]
    private int hyperEpisodes = 0;

    [ObservableProperty]
    private Chart? glucoseChart;

    // GraphicsView chart support 
    [ObservableProperty]
    private ObservableCollection<ChartDataPoint> measurements = new();

    [ObservableProperty]
    private bool showEmptyState = true;

    public GlucoseChartDrawable ChartDrawable { get; } = new GlucoseChartDrawable();

    // Контекст 
    private string _currentChildId = string.Empty;
    private string _selectedPeriod = "today";
    private List<MeasurementWithGlucose> _allMeasurements = new();
    private readonly ICryptoService _cryptoService;

    private static readonly SKColor ColorPrimary = SKColor.Parse("#42C0F5");
    private static readonly SKColor ColorDanger = SKColor.Parse("#C01527");

    public ChartPageViewModel(
        IMeasurementService measurementService,
        IMeasurementRepository measurementRepository,
        IStatisticsService statisticsService,
        IApiClient apiClient,
        ILogger<ChartPageViewModel> logger,
        IStorageService storageService,
        ICryptoService cryptoService)
    {
        _measurementService = measurementService;
        _measurementRepository = measurementRepository;
        _statisticsService = statisticsService;
        _apiClient = apiClient;
        _logger = logger;
        _storageService = storageService;
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Инициализация при загрузке страницы
    /// </summary>
    public async Task InitializeAsync()
    {
        var childId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);
        if (string.IsNullOrEmpty(childId))
        {
            _logger.LogInformation("Текущий ребёнок не выбран — график не загружен");
            IsLoading = false;
            TimeInRangeText = "� Выберите ребёнка в профиле";
            return;
        }
        await InitializeAsync(childId);
    }

    /// <summary>
    /// Инициализация для конкретного ребёнка
    /// </summary>
    public async Task InitializeAsync(string childId)
    {
        try
        {
            _logger.LogInformation("Инициализация ChartPage для: {ChildId}", childId);

            _currentChildId = childId;

            // Загружаем данные за выбранный период
            await LoadStatisticsAsync(_selectedPeriod);

            _logger.LogInformation("Инициализация завершена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации: {Message}", ex.Message);
            UpdateStatisticsUI(null);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Команда: Экспорт в PDF
    /// </summary>
    [RelayCommand]
    public async Task ExportToPdf(bool detailed = false)
    {
        try
        {
            _logger.LogInformation("Начинаем экспорт в PDF, подробный: {Detailed}", detailed);

            if (string.IsNullOrEmpty(_currentChildId))
            {
                _logger.LogWarning("ChildId не установлен для экспорта PDF");
                return;
            }

            // Определяем период для API
            var apiPeriod = _selectedPeriod switch
            {
                "today" => "day",
                "week" => "week", 
                "month" => "month",
                _ => "day"
            };

            // Вызываем API для генерации PDF
            var pdfBytes = await _apiClient.ExportStatisticsToPdfAsync(_currentChildId, apiPeriod, detailed);

            // Сохраняем PDF файл
            await SavePdfFileAsync(pdfBytes, detailed);

            _logger.LogInformation("PDF успешно экспортирован");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при экспорте PDF");
        }
    }

    /// <summary>
    /// Команда: Выбрать период
    /// </summary>
    [RelayCommand]
    public async Task SelectPeriod(string period)
    {
        try
        {
            _logger.LogInformation("Выбран период: {Period}", period);

            // Обновляем состояние кнопок
            IsTodaySelected = period == "today";
            IsWeekSelected = period == "week";
            IsMonthSelected = period == "month";

            _selectedPeriod = period;

            // Загружаем данные за выбранный период
            await LoadStatisticsAsync(period);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выборе периода");
        }
    }

    public void AddMeasurement(ChartDataPoint point)
    {
        Measurements.Add(point);
        ChartDrawable.DataPoints = Measurements.ToList();
        ShowEmptyState = Measurements.Count < 2;
    }

    public void SetMeasurements(IEnumerable<ChartDataPoint> points)
    {
        Measurements.Clear();
        foreach (var p in points)
            Measurements.Add(p);
        ChartDrawable.DataPoints = Measurements.ToList();
        ShowEmptyState = Measurements.Count < 2;
    }

    /// <summary>
    /// Загружает статистику за период
    /// </summary>
    private async Task LoadStatisticsAsync(string period)
    {
        try
        {
            IsLoading = true;
            _logger.LogInformation("Загрузка статистики за {Period}", period);

            if (string.IsNullOrEmpty(_currentChildId))
            {
                _logger.LogWarning("ChildId не установлен - используем тестовые данные");
                UpdateStatisticsUI(null);
                IsLoading = false;
                return;
            }

            DateTime startDate = period switch
            {
                "today" => DateTime.UtcNow.Date,
                "week" => DateTime.UtcNow.AddDays(-7).Date,
                "month" => DateTime.UtcNow.AddDays(-30).Date,
                _ => DateTime.UtcNow.Date
            };

            DateTime endDate = DateTime.UtcNow;

            // Получаем измерения из репозитория
            var measurements = await _measurementRepository.GetByDateRangeAsync(
                _currentChildId, startDate, endDate);

            _logger.LogInformation("Получено {Count} измерений за период {Period}", measurements.Count, period);

            // Параллельная расшифровка (Task.WhenAll) — раньше было последовательно
            // и при 100+ измерениях UI фризился на 5+ секунд.
            _allMeasurements.Clear();
            if (measurements.Count > 0)
            {
                var decryptTasks = measurements
                    .Select(m => SafeDecryptGlucoseAsync(m))
                    .ToList();

                var decryptedValues = await Task.WhenAll(decryptTasks);
                for (int i = 0; i < measurements.Count; i++)
                {
                    var glucose = decryptedValues[i];
                    if (glucose.HasValue)
                    {
                        _allMeasurements.Add(new MeasurementWithGlucose
                        {
                            Time = measurements[i].MeasurementTime,
                            Glucose = glucose.Value
                        });
                    }
                }
            }

            // Рассчитываем статистику с помощью StatisticsService
            var statistics = await _statisticsService.CalculateStatisticsAsync(measurements);

            // Обновляем UI с реальными данными
            UpdateStatisticsUI(statistics);

            GlucoseChart = BuildGlucoseChartValue();

            _logger.LogInformation("Статистика загружена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке статистики: {Message}", ex.Message);
            UpdateStatisticsUI(null);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Обновляет UI с данными статистики
    /// </summary>
    private void UpdateStatisticsUI(MeasurementStatistics? statistics)
    {
        if (statistics == null || statistics.MeasurementCount == 0)
        {
            // Показываем пустую статистику или тестовые данные
            TimeInRangePercent = 0.0;
            TimeInRangeText = "� Нет данных за выбранный период";
            TimeInRangeWidth = 0;

            AverageGlucose = 0.0;
            MinGlucose = 0.0;
            MaxGlucose = 0.0;
            MinMaxGlucose = "-- / --";

            MeasurementCount = 0;
            StandardDeviation = 0.0;

            HypoEpisodes = 0;
            HyperEpisodes = 0;

            _logger.LogInformation("Отображена пустая статистика");
            GlucoseChart = null;
            return;
        }

        // Обновляем UI с реальными данными
        TimeInRangePercent = statistics.TimeInTargetRange;
        
        // Определяем эмодзи и цвет на основе процента времени в норме
        string emoji = TimeInRangePercent switch
        {
            >= 70 => "�", // Отлично
            >= 50 => "�", // Хорошо
            _ => "�"       // Требует внимания
        };
        
        TimeInRangeText = $"{emoji} {TimeInRangePercent:F0}% времени в норме (4.0 - 10.0)";
        TimeInRangeWidth = (TimeInRangePercent / 100.0) * 240;  // Max width ~240px

        AverageGlucose = statistics.AverageGlucose;
        MinGlucose = statistics.MinGlucose;
        MaxGlucose = statistics.MaxGlucose;
        MinMaxGlucose = $"{MinGlucose:F1} / {MaxGlucose:F1}";

        MeasurementCount = statistics.MeasurementCount;
        StandardDeviation = statistics.StandardDeviation;

        HypoEpisodes = statistics.HypoEpisodes;
        HyperEpisodes = statistics.HyperEpisodes;

        _logger.LogInformation("Обновлена статистика: {MeasurementCount} измерений, среднее {AverageGlucose:F1}, в норме {TimeInRangePercent:F0}%",
            MeasurementCount, AverageGlucose, TimeInRangePercent);
    }

    /// <summary>
    /// Сохраняет PDF файл в папку загрузок или отправляет через Share
    /// </summary>
    private async Task SavePdfFileAsync(byte[] pdfBytes, bool detailed)
    {
        try
        {
            var fileName = detailed 
                ? $"SugarGuard_Detailed_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
                : $"SugarGuard_Report_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            // Создаём временный файл
            var tempFilePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(tempFilePath, pdfBytes);

            // Используем Share API для отправки/сохранения файла
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Экспорт статистики SugarGuard",
                File = new ShareFile(tempFilePath)
            });

            _logger.LogInformation("PDF файл сохранён: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении PDF файла");
        }
    }

    /// <summary>
    /// Безопасная расшифровка значения глюкозы для Task.WhenAll.
    /// Возвращает null при ошибке (битый ciphertext / невалидный формат).
    /// </summary>
    private async Task<double?> SafeDecryptGlucoseAsync(Measurement m)
    {
        try
        {
            var decrypted = await _cryptoService.DecryptAsync(m.EncryptedGlucoseValue);
            if (DoubleParser.TryParseDecrypted(decrypted, out var glucose))
            {
                return glucose;
            }
        }
        catch
        {
            // битый ciphertext — пропускаем
        }
        return null;
    }

    /// <summary>
    /// Строит линейный график глюкозы по _allMeasurements
    /// </summary>
    private Chart? BuildGlucoseChartValue()
    {
        if (_allMeasurements.Count == 0)
            return null;
        var ordered = _allMeasurements.OrderBy(m => m.Time).ToList();
        var entries = ordered.Select(m => new ChartEntry((float)m.Glucose)
        {
            Label = m.Time.ToString("HH:mm"),
            ValueLabel = m.Glucose.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            Color = m.Glucose > 10 || m.Glucose < 4 ? ColorDanger : ColorPrimary
        }).ToList();
        return new LineChart
        {
            Entries = entries,
            LineMode = LineMode.Spline,
            LineSize = 3f,
            PointMode = PointMode.Circle,
            PointSize = 6f
        };
    }

    /// <summary>
    /// Вспомогательный класс для работы с измерениями
    /// </summary>
    private class MeasurementWithGlucose
    {
        public DateTime Time { get; set; }
        public double Glucose { get; set; }
    }
}

