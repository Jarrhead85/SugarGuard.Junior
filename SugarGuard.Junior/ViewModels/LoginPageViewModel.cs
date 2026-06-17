using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel для страницы входа.
/// </summary>
public partial class LoginPageViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IStorageService _storageService;
    private readonly ILogger<LoginPageViewModel> _logger;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isLoading = false;

    public LoginPageViewModel(
        IAuthenticationService authenticationService,
        IStorageService storageService,
        ILogger<LoginPageViewModel> logger)
    {
        _authenticationService = authenticationService;
        _storageService = storageService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsLoading) return;

        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Введите email и пароль";
            return;
        }

        try
        {
            IsLoading = true;

            var success = await _authenticationService.LoginAsync(Email, Password);

            if (success)
            {
                _logger.LogInformation("Вход выполнен успешно для {Email}", Email);
                await NavigateAfterLoginAsync();
            }
            else
            {
                ErrorMessage = "Неверный email или пароль";
                _logger.LogWarning("Неверные учётные данные для {Email}", Email);
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            ErrorMessage = "Неверный email или пароль";
            _logger.LogWarning("401 при входе для {Email}", Email);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ошибка подключения. Проверьте интернет.";
            _logger.LogError(ex, "Ошибка при входе для {Email}", Email);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task NavigateAfterLoginAsync()
    {
        try
        {
            var isEmailVerified = await _authenticationService.IsEmailVerifiedAsync();

            if (!isEmailVerified)
            {
                _logger.LogInformation("Email не верифицирован, перенаправление на верификацию");
                await Shell.Current.GoToAsync($"//verifypage?email={Uri.EscapeDataString(Email)}");
                return;
            }

            var onboardingCompleted = await _storageService.GetAsync("onboarding_completed");
            if (onboardingCompleted != "true")
            {
                _logger.LogInformation("Онбординг не завершён, перенаправление на онбординг");
                await Shell.Current.GoToAsync("//onboardingpage");
                return;
            }

            await Shell.Current.GoToAsync("//mainpage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при навигации после входа");
            await Shell.Current.GoToAsync("//mainpage");
        }
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync("//registerpage");
    }
}
