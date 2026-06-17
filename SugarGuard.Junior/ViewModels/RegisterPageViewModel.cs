using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel экрана регистрации ребёнка.
/// </summary>
public partial class RegisterPageViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IStorageService _storageService;
    private readonly ILogger<RegisterPageViewModel> _logger;

    [ObservableProperty]
    private int currentStep;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private DateTime dateOfBirth = DateTime.Today.AddYears(-10);

    /// <summary>
    /// Максимальная дата для DatePicker.
    /// </summary>
    public DateTime MaxDate => DateTime.Today;

    [ObservableProperty]
    private string firstName = string.Empty;

    [ObservableProperty]
    private string lastName = string.Empty;

    [ObservableProperty]
    private bool parentConsentRequired;

    [ObservableProperty]
    private bool parentConsentGiven;

    [ObservableProperty]
    private bool useSmsVerification;

    [ObservableProperty]
    private string phoneNumber = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool canGoNext;

    public RegisterPageViewModel(
        IAuthenticationService authenticationService,
        IStorageService storageService,
        ILogger<RegisterPageViewModel> logger)
    {
        _authenticationService = authenticationService;
        _storageService = storageService;
        _logger = logger;
    }

    partial void OnEmailChanged(string value) => ValidateStep1();

    partial void OnPasswordChanged(string value) => ValidateStep1();

    partial void OnConfirmPasswordChanged(string value) => ValidateStep1();

    partial void OnDateOfBirthChanged(DateTime value)
    {
        ParentConsentRequired = CalculateAge(value) < 14;
        ValidateStep1();
    }

    partial void OnParentConsentGivenChanged(bool value) => ValidateStep1();

    partial void OnFirstNameChanged(string value) => ValidateStep1();

    private void ValidateStep1()
    {
        var isEmailValid = Utilities.Validators.IsValidEmail(Email);
        var passwordValidation = Utilities.Validators.IsValidPassword(Password);
        var isPasswordValid = passwordValidation.isValid && Password == ConfirmPassword;
        var hasConsent = !ParentConsentRequired || ParentConsentGiven;
        var hasName = !string.IsNullOrWhiteSpace(FirstName);

        CanGoNext = isEmailValid && isPasswordValid && hasConsent && hasName;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void GoToNextStep()
    {
        if (CurrentStep < 2)
        {
            CurrentStep++;
        }
    }

    [RelayCommand]
    private void GoToPreviousStep()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
        }
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsLoading)
        {
            return;
        }

        ErrorMessage = string.Empty;

        if (!ValidateAll())
        {
            return;
        }

        try
        {
            IsLoading = true;

            var user = await _authenticationService.RegisterAsync(
                FirstName,
                LastName,
                Email,
                PhoneNumber,
                Password);

            if (user is not null)
            {
                _logger.LogInformation("Регистрация успешна для {Email}", Email);

                await SavePendingChildProfileAsync();
                try
                {
                await _storageService.SaveAsync("pending_child_last_name", string.IsNullOrWhiteSpace(LastName) ? "Ребенок" : LastName.Trim());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Registration succeeded, but legacy pending child last name was not saved.");
                }
                await NavigateToVerificationAsync();
            }
            else
            {
                ErrorMessage = "Ошибка при регистрации. Попробуйте позже.";
            }
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Ошибка при регистрации. Попробуйте позже."
                : ex.Message;
            _logger.LogWarning(ex, "Registration was rejected by API");
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Проверьте данные регистрации."
                : ex.Message;
            _logger.LogWarning(ex, "Registration validation failed");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = "Ошибка подключения. Проверьте интернет.";
            _logger.LogError(ex, "Network error during registration");
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = "Сервер не ответил вовремя. Попробуйте ещё раз.";
            _logger.LogError(ex, "Registration request timed out");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ошибка при регистрации. Попробуйте позже.";
            _logger.LogError(ex, "Registration failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SavePendingChildProfileAsync()
    {
        try
        {
            await _storageService.SaveAsync("pending_child_first_name", FirstName.Trim());
            await _storageService.SaveAsync("pending_child_last_name", string.IsNullOrWhiteSpace(LastName) ? "Ребенок" : LastName.Trim());
            await _storageService.SaveAsync("pending_child_date_of_birth", DateOfBirth.ToString("O"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Registration succeeded, but pending child profile was not saved.");
        }
    }

    private async Task NavigateToVerificationAsync()
    {
        try
        {
            var target = $"//verifypage?email={Uri.EscapeDataString(Email)}" +
                         $"&phone={Uri.EscapeDataString(PhoneNumber)}" +
                         $"&useSms={UseSmsVerification}";

            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(target));
        }
        catch (Exception ex)
        {
            ErrorMessage = "Код отправлен на email. Откройте экран подтверждения и введите код из письма.";
            _logger.LogError(ex, "Registration succeeded, but navigation to verification failed.");
        }
    }

    [RelayCommand]
    private async Task LoginWithYandexAsync()
    {
        ErrorMessage = "Вход через Яндекс пока не настроен. Используйте регистрацию по email.";
        await Task.CompletedTask;
    }

    private bool ValidateAll()
    {
        if (!Utilities.Validators.IsValidEmail(Email))
        {
            ErrorMessage = "Введите корректный email.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FirstName))
        {
            ErrorMessage = "Введите имя ребёнка.";
            return false;
        }

        var passwordValidation = Utilities.Validators.IsValidPassword(Password);
        if (!passwordValidation.isValid)
        {
            ErrorMessage = $"Пароль: {string.Join(", ", passwordValidation.errors)}.";
            return false;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Пароли не совпадают.";
            return false;
        }

        if (ParentConsentRequired && !ParentConsentGiven)
        {
            ErrorMessage = "Требуется согласие родителя.";
            return false;
        }

        return true;
    }

    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;

        if (birthDate.Date > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
