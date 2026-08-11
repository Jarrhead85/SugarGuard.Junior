using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
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
    private readonly IChildSessionBootstrapService _childSessionBootstrapService;
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
        IChildSessionBootstrapService childSessionBootstrapService,
        ILogger<LoginPageViewModel> logger)
    {
        _authenticationService = authenticationService;
        _storageService = storageService;
        _childSessionBootstrapService = childSessionBootstrapService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsLoading) return;

        ErrorMessage = string.Empty;

        try
        {
            IsLoading = true;

            // При повторном запуске не ждём сетевой тайм-аут. Connectivity на
            // Android иногда остаётся Internet в авиарежиме, поэтому сначала
            // всегда проверяем исключительно локальный, подтверждённый сеанс.
            if (await _authenticationService.CanResumeOfflineSessionAsync())
            {
                await ResumeSavedOfflineSessionAsync();
                return;
            }

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await ResumeSavedOfflineSessionAsync();
                return;
            }

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Введите email и пароль";
                return;
            }

            var success = await _authenticationService.LoginAsync(Email, Password);

            if (success)
            {
                _logger.LogInformation("Вход выполнен успешно.");
                await NavigateAfterLoginAsync();
            }
            else
            {
                ErrorMessage = "Неверный email или пароль";
                _logger.LogWarning("Неверные учётные данные.");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            ErrorMessage = "Неверный email или пароль";
            _logger.LogWarning("Сервер отклонил учётные данные при входе.");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = "Не удалось подключиться к сервису. Проверьте интернет и попробуйте снова.";
            _logger.LogWarning(ex, "Сервис недоступен при входе.");
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = "Сервис не ответил вовремя. Проверьте интернет и попробуйте снова.";
            _logger.LogWarning(ex, "Истёк таймаут при входе.");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ошибка подключения. Проверьте интернет.";
            _logger.LogError(ex, "Ошибка при входе.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// The app normally restores this session during startup. This fallback keeps
    /// a child out of the login trap if an older build stored the user id under a
    /// different SecureStorage prefix and the repair only happens after this page
    /// has already appeared.
    /// </summary>
    private async Task ResumeSavedOfflineSessionAsync()
    {
        var savedEmail = await _storageService.GetAsync("current_email");
        if (!string.IsNullOrWhiteSpace(Email) &&
            !string.IsNullOrWhiteSpace(savedEmail) &&
            !string.Equals(savedEmail.Trim(), Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Нет подключения. Сменить аккаунт можно после подключения к интернету.";
            return;
        }

        if (!await _authenticationService.CanResumeOfflineSessionAsync())
        {
            ErrorMessage = "Нет подключения. Для первого входа на этом телефоне нужен интернет.";
            return;
        }

        _logger.LogInformation("Восстановлена сохранённая офлайн-сессия.");
        await NavigateAfterLoginAsync(preferLocalSession: true);
    }

    private async Task NavigateAfterLoginAsync(bool preferLocalSession = false)
    {
        try
        {
            var isOffline = preferLocalSession || Connectivity.Current.NetworkAccess != NetworkAccess.Internet;
            var isEmailVerified = isOffline || await _authenticationService.IsEmailVerifiedAsync();

            if (!isEmailVerified)
            {
                _logger.LogInformation("Email не верифицирован, перенаправление на верификацию");
                await Shell.Current.GoToAsync($"//verifypage?email={Uri.EscapeDataString(Email)}");
                return;
            }

            var onboardingCompleted = await _storageService.GetAsync("onboarding_completed");
            if (!string.Equals(onboardingCompleted, "true", StringComparison.OrdinalIgnoreCase))
            {
                if (!isOffline)
                {
                    var restored = await _childSessionBootstrapService.EnsureChildSessionAsync();
                    if (!restored)
                    {
                        _logger.LogInformation("Онбординг не завершён и серверный профиль ребёнка не найден, перенаправление на онбординг");
                        await Shell.Current.GoToAsync("//onboardingpage");
                        return;
                    }
                }
            }
            else if (!isOffline)
            {
                await _childSessionBootstrapService.EnsureChildSessionAsync();
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
