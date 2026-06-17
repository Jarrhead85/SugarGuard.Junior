using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
