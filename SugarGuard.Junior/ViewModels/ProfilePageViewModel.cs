// ViewModel страницы профиля
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Models.Enums;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel страницы профиля
/// </summary>
public partial class ProfilePageViewModel : ObservableObject
{
    private readonly ILogger<ProfilePageViewModel> _logger;
    private readonly IChildRepository _childRepository;
    private readonly IDiabetesSettingsRepository _diabetesSettingsRepository;
    private readonly IStorageService _storageService;
    private readonly ISyncService _syncService;
    private readonly IEditProfilePageFactory _editProfilePageFactory;
    private readonly IDiabetesSettingsPageFactory _diabetesSettingsPageFactory;
    private readonly IAccessManagementPageFactory _accessManagementPageFactory;
    private readonly IApiClient _apiClient;
    private readonly IAuthenticationService _authenticationService;
    private readonly IThemeService _themeService;
    private readonly INotificationService _notificationService;
    private bool _isLoadingPreferences;

    // OBSERVABLE PROPERTIES

    // --- Информация о ребёнке ---
    [ObservableProperty]
    private string childName = "Профиль не настроен";

    [ObservableProperty]
    private int childAge = 0;

    [ObservableProperty]
    private string childDiagnosis = "Заполни данные в профиле";

    // --- Telegram ---
    [ObservableProperty]
    private string telegramStatus = "Не подключен";

    [ObservableProperty]
    private string telegramStatusColor = "#A7A9A9";

    [ObservableProperty]
    private string telegramButtonText = "Подключить";

    [ObservableProperty]
    private string telegramButtonColor = "#42C0F5";

    [ObservableProperty]
    private bool isTelegramConnected = false;

    // --- Apple Health ---
    [ObservableProperty]
    private string healthKitStatus = " Не подключен";

    [ObservableProperty]
    private string healthKitStatusColor = "#A7A9A9";

    [ObservableProperty]
    private string healthKitButtonText = "Подключить";

    [ObservableProperty]
    private string healthKitButtonColor = "#42C0F5";

    [ObservableProperty]
    private bool isHealthKitConnected = false;

    // --- Настройки ---
    [ObservableProperty]
    private string targetRangeText = "4.0 - 10.0 ммоль/л";

    [ObservableProperty]
    private bool notificationsEnabled = true;

    [ObservableProperty]
    private bool darkThemeEnabled = false;

    // --- Информация ---
    [ObservableProperty]
    private string appVersion = "1.0.0";

    [ObservableProperty]
    private string lastSyncTime = "Локальные данные";

    [ObservableProperty]
    private bool isEmailVerified = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSmallScaleSelected))]
    [NotifyPropertyChangedFor(nameof(IsDefaultScaleSelected))]
    [NotifyPropertyChangedFor(nameof(IsLargeScaleSelected))]
    private ScalePreset currentScale = ScalePreset.Default;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSkinText))]
    [NotifyPropertyChangedFor(nameof(IsNeutralSkinSelected))]
    [NotifyPropertyChangedFor(nameof(IsBoySkinSelected))]
    [NotifyPropertyChangedFor(nameof(IsGirlSkinSelected))]
    private InterfaceSkin currentSkin = InterfaceSkin.Neutral;

    public bool IsSmallScaleSelected => CurrentScale == ScalePreset.Small;
    public bool IsDefaultScaleSelected => CurrentScale == ScalePreset.Default;
    public bool IsLargeScaleSelected => CurrentScale == ScalePreset.Large;
    public bool IsNeutralSkinSelected => CurrentSkin == InterfaceSkin.Neutral;
    public bool IsBoySkinSelected => CurrentSkin == InterfaceSkin.Boy;
    public bool IsGirlSkinSelected => CurrentSkin == InterfaceSkin.Girl;

    public string CurrentSkinText => CurrentSkin switch
    {
        InterfaceSkin.Boy => "Для мальчика",
        InterfaceSkin.Girl => "Для девочки",
        _ => "Нейтральный"
    };

    // --- Контекст ---
    private string _currentChildId = string.Empty;

    public ProfilePageViewModel(
        ILogger<ProfilePageViewModel> logger,
        IChildRepository childRepository,
        IDiabetesSettingsRepository diabetesSettingsRepository,
        IStorageService storageService,
        ISyncService syncService,
        IEditProfilePageFactory editProfilePageFactory,
        IDiabetesSettingsPageFactory diabetesSettingsPageFactory,
        IAccessManagementPageFactory accessManagementPageFactory,
        IApiClient apiClient,
        IAuthenticationService authenticationService,
        IThemeService themeService,
        INotificationService notificationService)
    {
        _logger = logger;
        _childRepository = childRepository;
        _diabetesSettingsRepository = diabetesSettingsRepository;
        _storageService = storageService;
        _syncService = syncService;
        _editProfilePageFactory = editProfilePageFactory;
        _diabetesSettingsPageFactory = diabetesSettingsPageFactory;
        _accessManagementPageFactory = accessManagementPageFactory;
        _apiClient = apiClient;
        _authenticationService = authenticationService;
        _themeService = themeService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Инициализация при загрузке страницы (читает текущего ребёнка из storage)
    /// </summary>
    public async Task InitializeAsync()
    {
        AppVersion = AppInfo.Current.VersionString;

        _isLoadingPreferences = true;
        try
        {
            DarkThemeEnabled = Preferences.Get("dark_theme_enabled", false);
            NotificationsEnabled = Preferences.Get("notifications_enabled", true);

            var savedScale = (ScalePreset)Preferences.Get("interface_scale", (int)ScalePreset.Default);
            CurrentScale = savedScale;
            _themeService.ApplyScale(savedScale);

            var savedSkin = (InterfaceSkin)Preferences.Get("interface_skin", (int)InterfaceSkin.Neutral);
            CurrentSkin = savedSkin;
            _themeService.ApplySkin(savedSkin, DarkThemeEnabled);
        }
        finally
        {
            _isLoadingPreferences = false;
        }

        var lastSync = await _storageService.GetAsync("last_sync_time");
        if (DateTime.TryParse(lastSync, null, DateTimeStyles.RoundtripKind, out var lastSyncUtc))
            LastSyncTime = $"Последняя синхронизация: {lastSyncUtc.ToLocalTime():dd.MM.yyyy HH:mm}";

        var childId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);
        if (string.IsNullOrEmpty(childId))
        {
            _logger.LogInformation("No child selected - profile not loaded");
            ChildName = "Профиль не настроен";
            ChildAge = 0;
            ChildDiagnosis = "Сначала заверши настройку профиля";
            return;
        }
        await InitializeAsync(childId);
    }

    /// <summary>
    /// Инициализация для выбранного ребёнка (сохраняет в storage и загружает данные)
    /// </summary>
    public async Task InitializeAsync(string childId)
    {
        try
        {
            _logger.LogInformation("ProfilePage initializing for child {ChildId}", childId);

            _currentChildId = childId;
            await _storageService.SaveAsync(AppConstants.StorageKeyCurrentChildId, childId);

            // Загружаем данные профиля
            await LoadProfileDataAsync();

            _logger.LogInformation("ProfilePage initialization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProfilePage initialization failed for child {ChildId}", _currentChildId);
        }
    }

    /// <summary>
    /// Команда: Добавить нового ребёнка (создаёт запись и открывает редактирование)
    /// </summary>
    [RelayCommand]
    public async Task AddChild()
    {
        try
        {
            _logger.LogInformation("Adding new child");

            var parentUserId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentUserId);
            if (string.IsNullOrEmpty(parentUserId))
                parentUserId = "local-user";

            var newChildId = Guid.NewGuid().ToString();
            var page = _editProfilePageFactory.CreateNew(newChildId, parentUserId);
            if (Shell.Current != null)
                await Shell.Current.Navigation.PushModalAsync(page);
            _logger.LogInformation("Edit form opened for new child draft");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding new child");
            await DisplayAlert("Ошибка", $"Не удалось добавить ребёнка: {ex.Message}", "ОК");
        }
    }

    /// <summary>
    /// Команда: Редактировать профиль
    /// </summary>
    [RelayCommand]
    public async Task EditProfile()
    {
        try
        {
            _logger.LogInformation("Opening profile edit");

            var childId = !string.IsNullOrEmpty(_currentChildId)
                ? _currentChildId
                : await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);
            if (string.IsNullOrEmpty(childId))
            {
                await DisplayAlert("Ошибка", "Сначала выберите ребёнка", "ОК");
                return;
            }
            var page = _editProfilePageFactory.Create(childId);
            await Shell.Current.Navigation.PushModalAsync(page);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening profile edit");
            await DisplayAlert("Ошибка", ex.Message, "ОК");
        }
    }

    /// <summary>
    /// Команда: Подключить Telegram
    /// </summary>
    [RelayCommand]
    private async Task OpenAccessManagement()
    {
        try
        {
            var page = _accessManagementPageFactory.Create();
            await Shell.Current.Navigation.PushModalAsync(page);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening access management page");
            await DisplayAlert("Доступ", "Не удалось открыть экран привязки. Попробуй ещё раз.", "ОК");
        }
    }

    [RelayCommand]
    public async Task ConnectTelegram()
    {
        try
        {
            _logger.LogInformation("Connecting Telegram");

            if (IsTelegramConnected)
            {
                IsTelegramConnected = false;
                Preferences.Set("telegram_connected", false);
                ApplyTelegramStatusUi();
                _logger.LogInformation("Telegram disconnected");
                return;
            }

            if (string.IsNullOrEmpty(_currentChildId))
            {
                await DisplayAlert("Ошибка", "Сначала выберите ребёнка в профиле.", "ОК");
                return;
            }

            var response = await _apiClient.GenerateTelegramCodeAsync(_currentChildId);
            if (response.Success && !string.IsNullOrEmpty(response.ConnectionCode))
            {
                await DisplayAlert(
                    "Код для бота",
                    $"Введите код в боте @SugarGuardBot:\n\n{response.ConnectionCode}\n\nСтатус «подключено» появится после подтверждения в боте. Код действителен {response.ExpiresIn / 60} мин.",
                    "ОК");
            }
            else
            {
                await DisplayAlert(
                    "Telegram",
                    "Не удалось получить код подключения. Проверь интернет и попробуй ещё раз.",
                    "ОК");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting Telegram for child {ChildId}", _currentChildId);
            await DisplayAlert("Telegram", "Не удалось получить код подключения. Попробуй ещё раз позже.", "ОК");
        }
    }

    /// <summary>
    /// Команда: Подключить Apple Health
    /// </summary>
    [RelayCommand]
    public async Task ConnectHealthKit()
    {
        try
        {
            _logger.LogInformation("Connecting Apple Health");

            if (IsHealthKitConnected)
            {
                IsHealthKitConnected = false;
                Preferences.Set("healthkit_connected", false);
                ApplyHealthKitStatusUi();
                _logger.LogInformation("Apple Health disconnected");
                return;
            }

            await DisplayAlert(
                "Apple Health",
                "На этом устройстве Apple Health недоступен. На iPhone приложение запросит разрешение и покажет статус подключения здесь.",
                "ОК");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при подключении HealthKit");
            await DisplayAlert("Ошибка", $"Не удалось подключить HealthKit: {ex.Message}", "ОК");
        }
    }

    /// <summary>
    /// Команда: Редактировать целевой диапазон
    /// </summary>
    [RelayCommand]
    public async Task EditTargetRange()
    {
        try
        {
            _logger.LogInformation("Opening target range edit");

            var childId = !string.IsNullOrEmpty(_currentChildId)
                ? _currentChildId
                : await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);
            if (string.IsNullOrEmpty(childId))
            {
                await DisplayAlert("Ошибка", "Сначала выберите ребёнка", "ОК");
                return;
            }
            var page = _diabetesSettingsPageFactory.Create(childId);
            await Shell.Current.Navigation.PushModalAsync(page);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening diabetes settings");
            await DisplayAlert("Ошибка", ex.Message, "ОК");
        }
    }

    /// <summary>
    /// Команда: Установить масштаб интерфейса
    /// </summary>
    [RelayCommand]
    public void SetScale(ScalePreset preset)
    {
        CurrentScale = preset;
        _themeService.ApplyScale(preset);
        Preferences.Set("interface_scale", (int)preset);
        _logger.LogInformation("Scale set to {Preset}", preset);
    }

    [RelayCommand]
    public void SetSkin(InterfaceSkin skin)
    {
        CurrentSkin = skin;
        _themeService.ApplySkin(skin, DarkThemeEnabled);
        Preferences.Set("interface_skin", (int)skin);
        _logger.LogInformation("Interface skin set to {Skin}", skin);
    }

    public async Task<bool> SetNotificationsEnabledAsync(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var permission = await Permissions.RequestAsync<Permissions.PostNotifications>();
                if (permission != PermissionStatus.Granted)
                {
                    NotificationsEnabled = false;
                    Preferences.Set("notifications_enabled", false);
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(_currentChildId))
                {
                    await _notificationService.ScheduleAllRemindersAsync(_currentChildId);
                }
            }
            else if (!string.IsNullOrWhiteSpace(_currentChildId))
            {
                await _notificationService.CancelAllRemindersAsync(_currentChildId);
            }

            NotificationsEnabled = enabled;
            Preferences.Set("notifications_enabled", enabled);
            _logger.LogInformation("Notifications setting changed. Enabled={Enabled}", enabled);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing notifications setting");
            return false;
        }
    }

    /// <summary>
    /// Команда: Синхронизировать сейчас
    /// </summary>
    [RelayCommand]
    public async Task SyncNow()
    {
        try
        {
            _logger.LogInformation("Starting sync");

            var success = await _syncService.SyncNowAsync();
            if (success)
            {
                await _storageService.SaveAsync("last_sync_time", DateTime.UtcNow.ToString("O"));
                LastSyncTime = "Синхронизирован прямо сейчас";
                await DisplayAlert("Успешно", "Данные синхронизированы", "ОК");
                _logger.LogInformation("Sync completed");
            }
            else
            {
                await DisplayAlert("Ошибка", "Не удалось синхронизировать данные", "ОК");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync error");
            await DisplayAlert("Ошибка", $"Ошибка синхронизации: {ex.Message}", "ОК");
        }
    }

    /// <summary>
    /// Команда: Выход
    /// </summary>
    [RelayCommand]
    public async Task Logout()
    {
        try
        {
            _logger.LogInformation("Logging out");

            // Подтверждение
            var confirmed = await DisplayAlert(
                "Выход",
                "Вы уверены, что хотите выйти?",
                "Да",
                "Отмена");

            if (confirmed)
            {
                await _authenticationService.LogoutAsync();
                await _storageService.ClearAsync();

                _logger.LogInformation("Logout completed");

                if (Shell.Current is not null)
                {
                    await Shell.Current.GoToAsync("//loginpage");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout error");
        }
    }

    /// <summary>
    /// ====== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ======
    /// </summary>

    /// <summary>
    /// Обработчик изменения переключателя темной темы
    /// </summary>
    partial void OnDarkThemeEnabledChanged(bool value)
    {
        if (_isLoadingPreferences)
        {
            return;
        }

        try
        {
            if (Application.Current is App app)
            {
                app.SwitchTheme(value);
            }
            _logger.LogInformation("Theme changed. Dark={IsDark}", value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing theme");
        }
    }

    /// <summary>
    /// Загружает данные профиля
    /// </summary>
    private async Task LoadProfileDataAsync()
    {
        try
        {
            _logger.LogInformation("Loading profile data");

            // Получаем данные ребенка из БД
            var child = await _childRepository.GetByIdAsync(_currentChildId);
            
            if (child != null)
            {
                // Получаем расшифрованное имя
                var firstName = await _childRepository.GetFirstNameAsync(child);
                ChildName = string.IsNullOrWhiteSpace(firstName) ? "Без имени" : firstName;
                ChildAge = child.AgeInYears;
                ChildDiagnosis = child.DiabetesType == Models.Enums.DiabetesType.Type1 
                    ? "Диабет 1 типа" 
                    : "Диабет 2 типа";

                var settings = await _diabetesSettingsRepository.GetByChildIdAsync(_currentChildId);
                if (settings is not null)
                {
                    var targetMin = await _diabetesSettingsRepository.GetDecryptedTargetRangeMinAsync(settings);
                    var targetMax = await _diabetesSettingsRepository.GetDecryptedTargetRangeMaxAsync(settings);
                    TargetRangeText = $"{targetMin:0.0} - {targetMax:0.0} ммоль/л";
                }
                
                _logger.LogInformation("Profile data loaded for {ChildName}", firstName);
            }
            else
            {
                _logger.LogWarning("Child profile not found, using default values");
                ChildName = "Профиль не настроен";
                ChildAge = 0;
                ChildDiagnosis = "Данные появятся после настройки";
            }

            // Загружаем статус верификации email
            try
            {
                IsEmailVerified = await _authenticationService.IsEmailVerifiedAsync();
            }
            catch
            {
                IsEmailVerified = true;
            }

            // Восстанавливаем сохранённое состояние (только если ранее была верификация)
            IsTelegramConnected = Preferences.Get("telegram_connected", false);
            IsHealthKitConnected = Preferences.Get("healthkit_connected", false);
            ApplyTelegramStatusUi();
            ApplyHealthKitStatusUi();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profile");
        }
    }

    private void ApplyTelegramStatusUi()
    {
        if (IsTelegramConnected)
        {
            TelegramStatus = "Подключен";
            TelegramStatusColor = "#42C0F5";
            TelegramButtonText = "Отключить";
            TelegramButtonColor = "#A84B2F";
        }
        else
        {
            TelegramStatus = "Не подключен";
            TelegramStatusColor = "#A7A9A9";
            TelegramButtonText = "Подключить";
            TelegramButtonColor = "#42C0F5";
        }
    }

    private void ApplyHealthKitStatusUi()
    {
        if (IsHealthKitConnected)
        {
            HealthKitStatus = "Подключен";
            HealthKitStatusColor = "#42C0F5";
            HealthKitButtonText = "Отключить";
            HealthKitButtonColor = "#A84B2F";
        }
        else
        {
            HealthKitStatus = "Не подключен";
            HealthKitStatusColor = "#A7A9A9";
            HealthKitButtonText = "Подключить";
            HealthKitButtonColor = "#42C0F5";
        }
    }

    /// <summary>
    /// Вспомогательный метод
    /// </summary>
    private static Task DisplayAlert(string title, string message, string ok)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page?.DisplayAlert(title, message, ok) ?? Task.CompletedTask;
    }

    private static Task<bool> DisplayAlert(string title, string message, string yes, string no)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page?.DisplayAlert(title, message, yes, no) ?? Task.FromResult(false);
    }
}
