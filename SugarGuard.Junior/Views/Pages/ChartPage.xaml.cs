using System.Diagnostics;

namespace SugarGuard.Junior.Views.Pages;

public partial class ChartPage : SwipeablePage
{
    private readonly ChartPageViewModel _viewModel;
    private bool _isInitialized;
    private double _lastInvalidatedWidth;
    private double _lastInvalidatedHeight;

    public ChartPage(ChartPageViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = viewModel;

        // Кастомный drawable к GraphicsView.
        GlucoseGraphicsView.Drawable = _viewModel.ChartDrawable;

        _viewModel.ChartDrawable.InvalidateCallback = InvalidateChart;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Запускаем пульсацию последней точки на графике
        _viewModel.ChartDrawable.AttachHost(GlucoseGraphicsView);

        // При первом открытии инициализируем данные страницы.
        if (!_isInitialized)
        {
            await InitializePageAsync();
            _isInitialized = true;
        }
        else
        {
            // При повторном открытии обновляем графическую область.
            InvalidateChart();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ChartDrawable.StopAnimation();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (Math.Abs(width - _lastInvalidatedWidth) < 0.5 &&
            Math.Abs(height - _lastInvalidatedHeight) < 0.5)
        {
            return;
        }

        _lastInvalidatedWidth = width;
        _lastInvalidatedHeight = height;

        // При смене размеров экрана перерисовываем график только если размер реально изменился.
        if (_isInitialized)
        {
            InvalidateChart();
        }
    }

    private async Task InitializePageAsync()
    {
        try
        {
            await _viewModel.InitializeAsync();

            // После загрузки данных сразу перерисовываем график
            InvalidateChart();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ChartPage InitializePageAsync error: {ex}");
            await DisplayAlert("Ошибка", $"Не удалось загрузить график: {ex.Message}", "ОК");
        }
    }

    private void InvalidateChart()
    {
        // Все обновления UI выполняем только в главном потоке.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            GlucoseGraphicsView?.Invalidate();
        });
    }
}
