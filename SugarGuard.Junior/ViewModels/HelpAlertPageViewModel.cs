using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel.Communication;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel страницы экстренной помощи (HelpAlertPage).
/// Позволяет ребёнку быстро позвонить родителю или в экстренные службы.
/// Перед звонком отправляет критический алерт через API.
/// Requirements: 5.1, 5.3, 5.4
/// </summary>
public partial class HelpAlertPageViewModel : ObservableObject
{
    private readonly IStorageService _storageService;
    private readonly INotificationService _notificationService;
    private readonly IApiClient _apiClient;
    private readonly ILogger<HelpAlertPageViewModel> _logger;

    [ObservableProperty]
    private bool isLoading = false;

    public HelpAlertPageViewModel(
        IStorageService storageService,
        INotificationService notificationService,
        IApiClient apiClient,
        ILogger<HelpAlertPageViewModel> logger)
    {
        _storageService = storageService;
        _notificationService = notificationService;
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// Звонок родителю. Читает номер из хранилища, отправляет API-алерт и открывает набор номера.
    /// Requirement 5.3
    /// </summary>
    [RelayCommand]
    private async Task CallParentAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;

            var phone = await _storageService.GetAsync(AppConstants.StorageKeyParentPhone);

            if (string.IsNullOrWhiteSpace(phone))
            {
                _logger.LogWarning("Номер телефона родителя не найден в хранилище");
                await ShowAlertAsync("Ошибка", "Номер телефона родителя не задан. Обратитесь к родителю для настройки.", "OK");
                return;
            }

            // Сначала отправляем API-алерт
            await SendCriticalApiAlertAsync();

            // Затем инициируем звонок
            PhoneDialer.Open(phone);
            _logger.LogInformation("Инициирован звонок родителю");
        }
        catch (FeatureNotSupportedException)
        {
            _logger.LogWarning("Функция звонка не поддерживается на этом устройстве");
            await ShowAlertAsync("Недоступно", "Звонки не поддерживаются на этом устройстве.", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при попытке позвонить родителю");
            await ShowAlertAsync("Ошибка", "Не удалось совершить звонок. Попробуйте ещё раз.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Отправляет критический алерт через API (уведомление родителю через Telegram/Push).
    /// Ошибка отправки не блокирует звонок.
    /// </summary>
    private async Task SendCriticalApiAlertAsync()
    {
        try
        {
            // Получаем актуальное (последнее известное) значение глюкозы из кэша.
            // При отсутствии данных — fallback на 0d (без замера).
            var glucoseValue = await GetLatestGlucoseValueAsync();

            await _notificationService.SendCriticalAlertAsync(
                title: "Экстренный вызов",
                message: "Ребёнок нажал кнопку экстренной помощи и звонит вам.",
                glucoseValue: glucoseValue);

            // Отправляем критический алерт родителям через API
            var childId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);

            var criticalRequest = new CriticalAlertRequest
            {
                ChildId = childId ?? string.Empty,
                GlucoseValue = glucoseValue,
                MeasurementTime = DateTime.UtcNow
            };

            var alertResult = await _apiClient.SendCriticalAlertAsync(criticalRequest);
            if (!alertResult)
                _logger.LogWarning("HelpAlert: не удалось отправить критический алерт через API.");
        }
        catch (Exception ex)
        {
            // Ошибка отправки алерта не должна блокировать звонок
            _logger.LogError(ex, "Ошибка отправки критического алерта при экстренном вызове");
        }
    }

    /// <summary>
    /// Читает последнее известное значение глюкозы из storage (записывается MainPageViewModel).
    /// При отсутствии/невалидном значении возвращает 0d.
    /// </summary>
    private async Task<double> GetLatestGlucoseValueAsync()
    {
        try
        {
            var raw = await _storageService.GetAsync(AppConstants.StorageKeyLastGlucoseValue);
            if (!string.IsNullOrWhiteSpace(raw) && DoubleParser.TryParseDecrypted(raw, out var value))
                return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HelpAlert: не удалось прочитать последнее значение глюкозы из storage");
        }
        return 0d;
    }

    /// <summary>
    /// Звонок в экстренные службы (112).
    /// Requirement 5.4
    /// </summary>
    [RelayCommand]
    private void CallEmergency()
    {
        try
        {
            PhoneDialer.Open("112");
            _logger.LogInformation("Инициирован звонок в экстренные службы (112)");
        }
        catch (FeatureNotSupportedException)
        {
            _logger.LogWarning("Функция звонка не поддерживается на этом устройстве");
            MainThread.BeginInvokeOnMainThread(async () =>
                await ShowAlertAsync("Недоступно", "Звонки не поддерживаются на этом устройстве.", "OK"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при попытке позвонить в экстренные службы");
        }
    }

    private static Task ShowAlertAsync(string title, string message, string cancel)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
            return page.DisplayAlert(title, message, cancel);
        return Task.CompletedTask;
    }
}
