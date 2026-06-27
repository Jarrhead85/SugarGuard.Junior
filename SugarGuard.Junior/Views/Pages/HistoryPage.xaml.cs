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
            // Загружаем или обновляем историю при появлении страницы.
            await _viewModel.InitializeAsync();
            _viewModel.ChartDrawable.InvalidateCallback = () =>
                MainThread.BeginInvokeOnMainThread(() => HistoryChart?.Invalidate());
            _viewModel.ChartDrawable.AttachHost(HistoryChart);
            HistoryChart.Invalidate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HistoryPage OnAppearing error: {ex}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ChartDrawable.StopAnimation();
    }
}
