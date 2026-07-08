using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

public partial class NutritionTrackerPage : SwipeablePage
{
    private readonly NutritionTrackerPageViewModel _viewModel;

    public NutritionTrackerPage(NutritionTrackerPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
