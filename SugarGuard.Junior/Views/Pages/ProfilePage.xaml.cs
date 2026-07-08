using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

public partial class ProfilePage : SwipeablePage
{
    private readonly ProfilePageViewModel _viewModel;
    private readonly ILogger? _logger;

    private bool _isApplyingLocalSettings;

    public ProfilePage(ProfilePageViewModel viewModel, ILogger? logger = null)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _logger = logger;
        BindingContext = viewModel;

        // Применяем сохранённую тему как можно раньше
        ApplySavedThemePreference();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // При каждом открытии страницы подтягиваем локальные UI-настройки.
        LoadLocalUiPreferences();

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ProfilePage: ошибка инициализации");
            await DisplayAlert("Ошибка", $"Не удалось загрузить профиль: {ex.Message}", "ОК");
        }
    }

    /// <summary>
    /// Применяет сохранённую тему приложения.
    /// </summary>
    private static void ApplySavedThemePreference()
    {
        var isDark = Preferences.Get("dark_theme_enabled", false);

        if (Application.Current is App app)
        {
            app.SwitchTheme(isDark);
        }
    }

    /// <summary>
    /// Загружает будущие пользовательские UI-настройки из Preferences.
    /// </summary>
    private void LoadLocalUiPreferences()
    {
        try
        {
            _isApplyingLocalSettings = true;

            var isDarkTheme = Preferences.Get("dark_theme_enabled", false);
            var notificationsEnabled = Preferences.Get("notifications_enabled", true);
            var compactMode = Preferences.Get("ui_compact_mode", false);
            var reduceMotion = Preferences.Get("ui_reduce_motion", false);

            if (ThemeSwitch is not null)
            {
                ThemeSwitch.IsToggled = isDarkTheme;
            }

            if (NotificationsSwitch is not null)
            {
                NotificationsSwitch.IsToggled = notificationsEnabled;
            }

            if (CompactModeSwitch is not null)
            {
                CompactModeSwitch.IsToggled = compactMode;
            }

            if (ReduceMotionSwitch is not null)
            {
                ReduceMotionSwitch.IsToggled = reduceMotion;
            }

        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ProfilePage: не удалось загрузить локальные UI-настройки");
        }
        finally
        {
            _isApplyingLocalSettings = false;
        }
    }

    /// <summary>
    /// Сохраняет флаг компактного режима.
    /// </summary>
    private void OnCompactModeToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingLocalSettings)
        {
            return;
        }

        try
        {
            Preferences.Set("ui_compact_mode", e.Value);
            _viewModel.SetScale(e.Value ? ScalePreset.Small : ScalePreset.Default);
            _logger?.LogInformation("ProfilePage: компактный режим изменён. Enabled={Enabled}", e.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ProfilePage: ошибка при сохранении компактного режима");
        }
    }

    /// <summary>
    /// Сохраняет настройку уменьшения анимаций.
    /// </summary>
    private void OnReduceMotionToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingLocalSettings)
        {
            return;
        }

        try
        {
            Preferences.Set("ui_reduce_motion", e.Value);
            _logger?.LogInformation("ProfilePage: режим снижения анимаций изменён. Enabled={Enabled}", e.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ProfilePage: ошибка при сохранении настройки анимаций");
        }
    }

    private async void OnOpenChartClicked(object sender, EventArgs e)
    {
        try
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("chartpage");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ProfilePage: ошибка перехода на график");
        }
    }

    private async void OnOpenScheduleClicked(object sender, EventArgs e)
    {
        try
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("schedulepage");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ProfilePage: ошибка перехода на расписание");
        }
    }

    /// <summary>
    /// Обработчик переключателя тёмной темы.
    /// Сохраняет выбор в Preferences и применяет тему немедленно.
    /// </summary>
    private void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingLocalSettings)
        {
            return;
        }

        try
        {
            var isDark = e.Value;
            if (_viewModel.DarkThemeEnabled != isDark)
            {
                _viewModel.DarkThemeEnabled = isDark;
            }

            if (Application.Current is App app)
            {
                app.SwitchTheme(isDark);
            }

            _logger?.LogInformation("ProfilePage: тема изменена. Тёмная={IsDark}", isDark);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ProfilePage: ошибка при переключении темы");
        }
    }

    private async void OnNotificationsToggled(object sender, ToggledEventArgs e)
    {
        if (_isApplyingLocalSettings)
        {
            return;
        }

        var applied = await _viewModel.SetNotificationsEnabledAsync(e.Value);
        if (applied || !e.Value)
        {
            return;
        }

        try
        {
            _isApplyingLocalSettings = true;
            NotificationsSwitch.IsToggled = false;
        }
        finally
        {
            _isApplyingLocalSettings = false;
        }

        await DisplayAlert(
            "Уведомления",
            "Разрешение не выдано. Его можно включить в системных настройках приложения.",
            "ОК");
    }
}
