using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel для экрана верификации email/SMS.
/// Управляет вводом кода формата ABCD-1234, таймером повторной отправки и обработкой ошибок.
/// </summary>
public partial class VerifyPageViewModel : ObservableObject, IQueryAttributable
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IStorageService _storageService;
    private readonly ILogger<VerifyPageViewModel> _logger;

    private IDispatcherTimer? _countdownTimer;
    private int _countdownSeconds;
    private string? _lastSubmittedCode;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string phoneNumber = string.Empty;

    [ObservableProperty]
    private bool useSms;

    [ObservableProperty]
    private string code = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isVerified;

    [ObservableProperty]
    private int remainingAttempts = 5;

    [ObservableProperty]
    private bool canResend;

    [ObservableProperty]
    private string resendButtonText = "Отправить повторно через 120 с";

    public VerifyPageViewModel(
        IAuthenticationService authenticationService,
        IStorageService storageService,
        ILogger<VerifyPageViewModel> logger)
    {
        _authenticationService = authenticationService;
        _storageService = storageService;
        _logger = logger;

        StartCountdown(120);
    }

    /// <summary>Инициализирует или обновляет параметры при навигации.</summary>
    public void Initialize(string email, string phone, bool useSms)
    {
        Email = email;
        PhoneNumber = phone;
        UseSms = useSms;
        Code = string.Empty;
        ErrorMessage = string.Empty;
        IsVerified = false;
        RemainingAttempts = 5;
    }

    /// <summary>
    /// IQueryAttributable: получает параметры из Shell-навигации.
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var email = query.TryGetValue("email", out var e) ? e?.ToString() ?? "" : "";
        var phone = query.TryGetValue("phone", out var p) ? p?.ToString() ?? "" : "";
        var useSms = query.TryGetValue("useSms", out var s) && bool.TryParse(s?.ToString(), out var sms) && sms;
        Initialize(email, phone, useSms);
    }

    partial void OnCodeChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            ErrorMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task VerifyCodeAsync()
    {
        var normalizedCode = ConnectionCodeFormat.Normalize(Code);

        if (IsLoading)
        {
            return;
        }

        if (!ConnectionCodeFormat.IsValid(normalizedCode, normalize: false))
        {
            ErrorMessage = "Введите все 8 символов кода из письма.";
            return;
        }

        if (string.Equals(_lastSubmittedCode, normalizedCode, StringComparison.Ordinal))
        {
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            IsLoading = true;
            _lastSubmittedCode = normalizedCode;
            var emailToVerify = await ResolveEmailAsync();

            if (string.IsNullOrWhiteSpace(emailToVerify))
            {
                ErrorMessage = "Email не найден. Вернитесь на экран входа и попробуйте снова.";
                return;
            }

            var verification = await _authenticationService.VerifyEmailAsync(emailToVerify, normalizedCode!);

            if (verification.IsValid || verification.Success)
            {
                IsVerified = true;
                _logger.LogInformation("Email успешно верифицирован");
                _countdownTimer?.Stop();

                await Shell.Current.GoToAsync("//onboardingpage");
            }
            else
            {
                _lastSubmittedCode = null;
                var serverMessage = verification.ErrorMessage ?? verification.Message;
                var shouldCountAttempt = string.IsNullOrWhiteSpace(serverMessage)
                    || serverMessage.Contains("Неверный код", StringComparison.OrdinalIgnoreCase);

                if (shouldCountAttempt)
                {
                    RemainingAttempts--;
                }

                ErrorMessage = !string.IsNullOrWhiteSpace(serverMessage)
                    ? serverMessage
                    : RemainingAttempts > 0
                        ? $"Неверный код. Осталось попыток: {RemainingAttempts}"
                        : "Слишком много неверных попыток. Запросите новый код.";

                _logger.LogWarning("Неверный код верификации. Осталось попыток: {RemainingAttempts}", RemainingAttempts);
            }
        }
        catch (Exception ex)
        {
            _lastSubmittedCode = null;
            ErrorMessage = "Ошибка проверки кода. Проверьте интернет.";
            _logger.LogError(ex, "Ошибка при верификации кода");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ResendCodeAsync()
    {
        if (IsLoading || !CanResend)
        {
            return;
        }

        try
        {
            IsLoading = true;
            var emailToVerify = await ResolveEmailAsync();

            if (string.IsNullOrWhiteSpace(emailToVerify))
            {
                ErrorMessage = "Email не найден. Вернитесь на экран входа и попробуйте снова.";
                return;
            }

            var success = UseSms
                ? false
                : await _authenticationService.SendEmailVerificationCodeAsync(emailToVerify);

            if (success)
            {
                _logger.LogInformation("Код верификации отправлен повторно");
                RemainingAttempts = 5;
                ErrorMessage = string.Empty;
                StartCountdown(120);
            }
            else
            {
                ErrorMessage = "Не удалось отправить код. Попробуйте позже.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ошибка отправки кода. Проверьте интернет.";
            _logger.LogError(ex, "Ошибка при повторной отправке кода");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ChangeEmailAsync()
    {
        _countdownTimer?.Stop();
        await Shell.Current.GoToAsync("..");
    }

    private async Task<string> ResolveEmailAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            Email = await _storageService.GetAsync("current_email") ?? string.Empty;
        }

        return Email.Trim().ToLowerInvariant();
    }

    /// <summary>Запускает обратный отсчёт для кнопки повторной отправки.</summary>
    private void StartCountdown(int seconds)
    {
        CanResend = false;
        _countdownSeconds = seconds;
        ResendButtonText = $"Отправить повторно через {seconds} с";

        _countdownTimer?.Stop();
        _countdownTimer = Application.Current!.Dispatcher.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.IsRepeating = true;
        _countdownTimer.Tick += (s, e) =>
        {
            _countdownSeconds--;

            if (_countdownSeconds <= 0)
            {
                _countdownTimer.Stop();
                CanResend = true;
                ResendButtonText = "Отправить повторно";
            }
            else
            {
                ResendButtonText = $"Отправить повторно через {_countdownSeconds} с";
            }
        };
        _countdownTimer.Start();
    }
}

