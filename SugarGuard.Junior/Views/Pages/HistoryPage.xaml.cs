using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Страница истории измерений глюкозы.
/// </summary>
public partial class HistoryPage : SwipeablePage
{
    private readonly HistoryPageViewModel _viewModel;

    public HistoryPage(HistoryPageViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            _viewModel.ChartDrawable.InvalidateCallback = () =>
                MainThread.BeginInvokeOnMainThread(() => HistoryChart?.Invalidate());
            _viewModel.ChartDrawable.AttachHost(HistoryChart);
            await _viewModel.InitializeAsync();
            HistoryChart.Invalidate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HistoryPage OnAppearing error: {ex}");
            _viewModel.SetLoadError("Не удалось загрузить историю. Проверь подключение и открой экран ещё раз.");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ChartDrawable.StopAnimation();
    }
}
