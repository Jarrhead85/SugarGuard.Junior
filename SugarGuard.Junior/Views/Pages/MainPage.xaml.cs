using Microsoft.Extensions.Logging;
using SugarGuard.Junior.ViewModels;
using SugarGuard.Junior.Views.Components;

namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Code-behind главного экрана.
/// </summary>
public partial class MainPage : SwipeablePage
{
    private readonly MainPageViewModel _viewModel;
    private readonly ILogger<MainPage> _logger;

    /// <summary>
    /// Флаг защиты от повторной инициализации при быстром появлении/исчезновении страницы 
    /// </summary>
    private bool _isInitialized;

    public MainPage(MainPageViewModel viewModel, ILogger<MainPage> logger)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _logger = logger;
        BindingContext = _viewModel;
    }

    /// <summary>
    /// Вызывается каждый раз, когда страница появляется на экране.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (!_isInitialized)
            {
                // Первичная инициализация: загрузить данные и подготовить график.
                await _viewModel.InitializeAsync();
                AttachMiniChart();
                _isInitialized = true;
            }
            else
            {
                // Возврат на страницу: перерисовать график с актуальными данными.
                InvalidateMiniChart();
            }

            // Таймер статуса запускается при каждом появлении страницы,
            // чтобы отображать актуальные время и «давность» последнего измерения.
            _viewModel.StartStatusTimer();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации MainPage");
        }
    }

    /// <summary>
    /// Вызывается при уходе со страницы
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopStatusTimer();

        if (_viewModel.MiniChartDrawable is { } drawable)
            drawable.StopAnimation();

        // MAUI Shell не разрушает Singleton Page при tab switching — OnDisappearing
        // вызывается при уходе в другую tab'у, но Page и ViewModel продолжают жить.
        // Подписки на _syncService (Singleton) живут до конца процесса — это нормально,
        // т.к. утечки между Singleton-объектами не бывает (всё умирает при process exit).
    }

    // Мини-график 
    private void AttachMiniChart()
    {
        if (GlucoseGraphicsMini is null)
        {
            _logger.LogWarning("GlucoseGraphicsMini не найден в дереве XAML — мини-график не подключён");
            return;
        }

        var drawable = _viewModel.MiniChartDrawable;

        if (drawable is null)
        {
            _logger.LogInformation("MiniChartDrawable не готов — мини-график будет скрыт");
            return;
        }

        // Регистрируем callback
        drawable.InvalidateCallback = InvalidateMiniChart;

        GlucoseGraphicsMini.Drawable = drawable;
        drawable.AttachHost(GlucoseGraphicsMini);

        _logger.LogInformation("Мини-график подключён к GlucoseGraphicsMini");
    }

    /// <summary>
    /// Запрашивает перерисовку мини-графика на главном потоке.
    /// </summary>
    private void InvalidateMiniChart()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            GlucoseGraphicsMini?.Invalidate();
        });
    }
}
