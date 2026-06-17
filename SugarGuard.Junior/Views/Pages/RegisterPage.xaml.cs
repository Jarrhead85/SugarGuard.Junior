using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnLoginTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//loginpage");
    }
}
