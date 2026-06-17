using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Страница срочного сценария помощи.
/// </summary>
public partial class HelpAlertPage : ContentPage
{
    // Храним ссылку на ViewModel
    private readonly HelpAlertPageViewModel _viewModel;

    /// <summary>
    /// Создает страницу экстренной помощи и назначает BindingContext.
    /// </summary>
    public HelpAlertPage(HelpAlertPageViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;
    }
}
