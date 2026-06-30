using Microsoft.Extensions.Logging;
using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

public partial class BackpackPage : SwipeablePage
{
    private readonly BackpackPageViewModel _viewModel;
    private readonly ILogger<BackpackPage>? _logger;
    private bool _isInitializing;

    public BackpackPage(BackpackPageViewModel viewModel, ILogger<BackpackPage>? logger = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _logger = logger;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isInitializing)
        {
            return;
        }

        _isInitializing = true;

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "BackpackPage failed to load.");
            await DisplayAlert("Рюкзак", "Не удалось загрузить рюкзак. Попробуй обновить экран ещё раз.", "ОК");
        }
        finally
        {
            _isInitializing = false;
        }
    }
}
