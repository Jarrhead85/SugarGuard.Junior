using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

public partial class SupportPage : ContentPage
{
    private readonly SupportPageViewModel _viewModel;

    public SupportPage(SupportPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
