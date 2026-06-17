// ViewModel для страницы настроек диабета
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel для настроек диабета
/// Поля: целевой диапазон, чувствительность к инсулину, коэффициент углеводов-инсулина, инсулины
/// </summary>
public partial class DiabetesSettingsPageViewModel : ObservableObject
{
    private readonly IDiabetesSettingsRepository _settingsRepository;
    private readonly ICryptoService _cryptoService;
    private DiabetesSettings _currentSettings = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetRangeText))]
    private string targetRangeMin = "4.0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetRangeText))]
    private string targetRangeMax = "10.0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetRangeText))]
    private string insulinSensitivity = "1.5";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetRangeText))]
    private string carbInsulinRatio = "10.0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetRangeText))]
    private string longActingDuration = "24";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetRangeText))]
    private string shortActingDuration = "4";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationErrors))]
    private string validationErrors = string.Empty;

    public DiabetesSettingsPageViewModel(IDiabetesSettingsRepository settingsRepository, ICryptoService cryptoService)
    {
        _settingsRepository = settingsRepository;
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Есть ли ошибки валидации
    /// </summary>
    public bool HasValidationErrors => !string.IsNullOrEmpty(ValidationErrors);

    /// <summary>
    /// Текст целевого диапазона для отображения
    /// </summary>
    public string TargetRangeText
    {
        get
        {
            if (DoubleParser.TryParseDecrypted(TargetRangeMin, out var min) && DoubleParser.TryParseDecrypted(TargetRangeMax, out var max))
            {
                return $"Целевой диапазон: {min:F1} - {max:F1} ммоль/л";
            }
            return "Целевой диапазон: — ммоль/л";
        }
    }

    partial void OnTargetRangeMinChanged(string value) => ValidateForm();
    partial void OnTargetRangeMaxChanged(string value) => ValidateForm();
    partial void OnInsulinSensitivityChanged(string value) => ValidateForm();
    partial void OnCarbInsulinRatioChanged(string value) => ValidateForm();
    partial void OnLongActingDurationChanged(string value) => ValidateForm();
    partial void OnShortActingDurationChanged(string value) => ValidateForm();

    /// <summary>
    /// Загружает настройки диабета для редактирования
    /// </summary>
    public async Task LoadSettingsAsync(string childId)
    {
        try
        {
            IsLoading = true;

            var settings = await _settingsRepository.GetByChildIdAsync(childId);

            if (settings != null)
            {
                _currentSettings = settings;

                try
                {
                    TargetRangeMin = !string.IsNullOrEmpty(settings.EncryptedTargetRangeMin)
                        ? await _cryptoService.DecryptAsync(settings.EncryptedTargetRangeMin)
                        : "4.0";
                    TargetRangeMax = !string.IsNullOrEmpty(settings.EncryptedTargetRangeMax)
                        ? await _cryptoService.DecryptAsync(settings.EncryptedTargetRangeMax)
                        : "10.0";
                    InsulinSensitivity = !string.IsNullOrEmpty(settings.EncryptedInsulinSensitivity)
                        ? await _cryptoService.DecryptAsync(settings.EncryptedInsulinSensitivity)
                        : "1.5";
                    CarbInsulinRatio = !string.IsNullOrEmpty(settings.EncryptedCarbInsulinRatio)
                        ? await _cryptoService.DecryptAsync(settings.EncryptedCarbInsulinRatio)
                        : "10.0";
                }
                catch (Exception)
                {
                    await Shell.Current.DisplayAlert("Предупреждение",
                        "Не удалось дешифровать некоторые настройки. Используются значения по умолчанию.", "OK");
                    TargetRangeMin = "4.0";
                    TargetRangeMax = "10.0";
                    InsulinSensitivity = "1.5";
                    CarbInsulinRatio = "10.0";
                }

                LongActingDuration = settings.LongActingDuration.ToString();
                ShortActingDuration = settings.ShortActingDuration.ToString();
            }
            else
            {
                _currentSettings = new DiabetesSettings
                {
                    ChildId = childId,
                    LongActingDuration = 24,
                    ShortActingDuration = 4
                };

                TargetRangeMin = "4.0";
                TargetRangeMax = "10.0";
                InsulinSensitivity = "1.5";
                CarbInsulinRatio = "10.0";
                LongActingDuration = "24";
                ShortActingDuration = "4";
            }

            OnPropertyChanged(nameof(TargetRangeText));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось загрузить настройки: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSave() => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            IsLoading = true;

            if (!DoubleParser.TryParseDecrypted(TargetRangeMin, out var targetMin) ||
                !DoubleParser.TryParseDecrypted(TargetRangeMax, out var targetMax) ||
                !DoubleParser.TryParseDecrypted(InsulinSensitivity, out var sensitivity) ||
                !DoubleParser.TryParseDecrypted(CarbInsulinRatio, out var ratio) ||
                !int.TryParse(LongActingDuration, out var longDuration) ||
                !int.TryParse(ShortActingDuration, out var shortDuration))
            {
                ValidationErrors = "Проверьте правильность введённых числовых значений";
                return;
            }

            if (targetMin >= targetMax)
            {
                ValidationErrors = "Минимальный уровень должен быть меньше максимального";
                return;
            }

            var encryptedTargetMin = await _cryptoService.EncryptAsync(targetMin.ToString("F1"));
            var encryptedTargetMax = await _cryptoService.EncryptAsync(targetMax.ToString("F1"));
            var encryptedSensitivity = await _cryptoService.EncryptAsync(sensitivity.ToString("F2"));
            var encryptedRatio = await _cryptoService.EncryptAsync(ratio.ToString("F2"));

            await _settingsRepository.UpdateEncryptedAsync(_currentSettings.ChildId, encryptedTargetMin, encryptedTargetMax, encryptedSensitivity, encryptedRatio, longDuration, shortDuration);

            await Shell.Current.DisplayAlert("Успех", "Настройки диабета успешно обновлены", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (FormatException)
        {
            ValidationErrors = "Проверьте правильность введённых числовых значений";
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось сохранить настройки: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        TargetRangeMin = "4.0";
        TargetRangeMax = "10.0";
        InsulinSensitivity = "1.5";
        CarbInsulinRatio = "10.0";
        LongActingDuration = "24";
        ShortActingDuration = "4";

        OnPropertyChanged(nameof(TargetRangeText));
    }

    private void ValidateForm()
    {
        var errors = new List<string>();

        if (!DoubleParser.TryParseDecrypted(TargetRangeMin, out var minTarget))
        {
            errors.Add("Введите корректный минимальный целевой уровень");
        }
        else if (minTarget <= 0 || minTarget > 30)
        {
            errors.Add("Минимальный целевой уровень должен быть от 0.1 до 30.0 ммоль/л");
        }

        if (!DoubleParser.TryParseDecrypted(TargetRangeMax, out var maxTarget))
        {
            errors.Add("Введите корректный максимальный целевой уровень");
        }
        else if (maxTarget <= 0 || maxTarget > 30)
        {
            errors.Add("Максимальный целевой уровень должен быть от 0.1 до 30.0 ммоль/л");
        }

        if (DoubleParser.TryParseDecrypted(TargetRangeMin, out var min) && DoubleParser.TryParseDecrypted(TargetRangeMax, out var max))
        {
            if (min >= max)
            {
                errors.Add("Минимальный уровень должен быть меньше максимального");
            }
        }

        if (!DoubleParser.TryParseDecrypted(InsulinSensitivity, out var sensitivity))
        {
            errors.Add("Введите корректную чувствительность к инсулину");
        }
        else if (sensitivity <= 0 || sensitivity > 10)
        {
            errors.Add("Чувствительность к инсулину должна быть от 0.1 до 10.0");
        }

        if (!DoubleParser.TryParseDecrypted(CarbInsulinRatio, out var ratio))
        {
            errors.Add("Введите корректный коэффициент углеводов-инсулина");
        }
        else if (ratio <= 0 || ratio > 100)
        {
            errors.Add("Коэффициент углеводов-инсулина должен быть от 0.1 до 100.0");
        }

        if (!int.TryParse(LongActingDuration, out var longDuration))
        {
            errors.Add("Введите корректную длительность длительного инсулина");
        }
        else if (longDuration < 12 || longDuration > 48)
        {
            errors.Add("Длительность длительного инсулина должна быть от 12 до 48 часов");
        }

        if (!int.TryParse(ShortActingDuration, out var shortDuration))
        {
            errors.Add("Введите корректную длительность быстрого инсулина");
        }
        else if (shortDuration < 2 || shortDuration > 8)
        {
            errors.Add("Длительность быстрого инсулина должна быть от 2 до 8 часов");
        }

        ValidationErrors = errors.Count > 0 ? string.Join("\n", errors) : string.Empty;
        OnPropertyChanged(nameof(TargetRangeText));
    }
}
