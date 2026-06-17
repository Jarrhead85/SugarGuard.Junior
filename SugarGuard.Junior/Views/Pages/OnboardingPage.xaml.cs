using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

public partial class OnboardingPage : ContentPage
{
    public OnboardingPage(OnboardingPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
