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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HistoryPage OnAppearing error: {ex}");
        }
    }
}
