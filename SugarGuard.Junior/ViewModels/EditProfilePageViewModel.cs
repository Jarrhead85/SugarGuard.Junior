// ViewModel для страницы редактирования профиля ребёнка
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel для редактирования профиля ребёнка
/// Поля: имя, фамилия, дата рождения, вес, рост, тип диабета
/// Автоматический расчёт ИМТ при изменении веса/роста
/// </summary>
public partial class EditProfilePageViewModel : ObservableObject
{
    private readonly IChildRepository _childRepository;
    private readonly IStorageService _storageService;
    private readonly IApiClient _apiClient;
    private Child _currentChild = new();
    private bool _isCreateMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool isLoading;

    [ObservableProperty]
    private string firstName = string.Empty;

    [ObservableProperty]
    private string lastName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Age))]
    private DateTime dateOfBirth = DateTime.Today.AddYears(-10);

    [ObservableProperty]
    private string weight = string.Empty;

    [ObservableProperty]
    private string height = string.Empty;

    [ObservableProperty]
    private DiabetesType diabetesType = DiabetesType.Type1;

    [ObservableProperty]
    private DateTime diagnosisDate = DateTime.Today;

    [ObservableProperty]
    private string insulinScheme = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto))]
    private string? photoUrl;

    [ObservableProperty]
    private bool isPhotoBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BMIText))]
    private double calculatedBMI;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationErrors))]
    private string validationErrors = string.Empty;

    public EditProfilePageViewModel(
        IChildRepository childRepository,
        IStorageService storageService,
        IApiClient apiClient)
    {
        _childRepository = childRepository;
        _storageService = storageService;
        _apiClient = apiClient;
    }

    /// <summary>
    /// Вычисленный возраст
    /// </summary>
    public int Age
    {
        get
        {
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Year;
            if (DateOfBirth > today.AddYears(-age))
                age--;
            return age;
        }
    }

    /// <summary>
    /// Текст ИМТ для отображения
    /// </summary>
    public string BMIText => CalculatedBMI > 0 ? $"ИМТ: {CalculatedBMI:F1}" : "ИМТ: —";

    /// <summary>
    /// Есть ли ошибки валидации
    /// </summary>
    public bool HasValidationErrors => !string.IsNullOrEmpty(ValidationErrors);

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoUrl);

    /// <summary>
    /// Список типов диабета для выбора (экземплярное свойство для привязки Picker)
    /// </summary>
    public IReadOnlyList<DiabetesType> DiabetesTypesList => DiabetesTypes;

    partial void OnFirstNameChanged(string value) => ValidateForm();
    partial void OnLastNameChanged(string value) => ValidateForm();
    partial void OnDateOfBirthChanged(DateTime value) => ValidateForm();
    partial void OnWeightChanged(string value)
    {
        CalculateBMI();
        ValidateForm();
    }
    partial void OnHeightChanged(string value)
    {
        CalculateBMI();
        ValidateForm();
    }
    partial void OnDiabetesTypeChanged(DiabetesType value) => ValidateForm();
    partial void OnDiagnosisDateChanged(DateTime value) => ValidateForm();
    /// <summary>
    /// Список типов диабета для выбора (статический список значений)
    /// </summary>
    public static readonly IReadOnlyList<DiabetesType> DiabetesTypes = new[] { DiabetesType.Type1, DiabetesType.Type2 };

    /// <summary>
    /// Загружает данные профиля для редактирования
    /// </summary>
    public async Task LoadProfileAsync(string childId)
    {
        try
        {
            IsLoading = true;
            _isCreateMode = false;

            var child = await _childRepository.GetByIdAsync(childId);
            if (child != null)
            {
                _currentChild = child;

                FirstName = await _childRepository.GetFirstNameAsync(child);
                LastName = await _childRepository.GetLastNameAsync(child);
                DateOfBirth = child.DateOfBirth;
                Weight = child.Weight.ToString("F1");
                Height = child.Height.ToString("F0");
                DiabetesType = child.DiabetesType;
                DiagnosisDate = child.DiagnosisDate;
                InsulinScheme = child.InsulinScheme;
                PhotoUrl = child.PhotoUrl;

                CalculateBMI();
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось загрузить профиль: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void StartNewChildDraft(string childId, string parentUserId)
    {
        _isCreateMode = true;
        _currentChild = new Child
        {
            ChildId = childId,
            ParentUserId = parentUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CurrentInsulins = "[]"
        };

        FirstName = string.Empty;
        LastName = string.Empty;
        DateOfBirth = DateTime.Today.AddYears(-8);
        Weight = "30";
        Height = "130";
        DiabetesType = DiabetesType.Type1;
        DiagnosisDate = DateTime.Today.AddYears(-2);
        InsulinScheme = string.Empty;
        PhotoUrl = null;
        ValidationErrors = string.Empty;
        CalculateBMI();
    }

    private bool CanSave() => !IsLoading;

    /// <summary>
    /// Парсинг числового значения с поддержкой разных культур.
    /// Принимает "32.5" (Invariant), "32,5" (ru-RU/de-DE) и "32.5" на любой локали.
    /// </summary>
    /// <remarks>
    /// Паттерн скопирован из <c>HistoryPageViewModel.TryParseGlucoseValue</c>,
    /// чтобы все пользовательские числовые поля в Junior-клиенте вели себя одинаково.
    /// </remarks>
    private static bool TryParseLocalizedDouble(string rawValue, out double value)
    {
        value = 0d;

        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        var normalized = rawValue.Trim();

        return
            double.TryParse(
                normalized,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value)
            || double.TryParse(
                normalized,
                NumberStyles.Any,
                CultureInfo.CurrentCulture,
                out value)
            || double.TryParse(
                normalized.Replace(',', '.'),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            IsLoading = true;

            if (!TryParseLocalizedDouble(Weight, out var weight))
            {
                ValidationErrors = "Введите корректное значение веса (например, 32.5 или 32,5).";
                return;
            }

            if (!TryParseLocalizedDouble(Height, out var height))
            {
                ValidationErrors = "Введите корректное значение роста (например, 145.5 или 145,5).";
                return;
            }

            var updatedChild = new Child
            {
                ChildId = _currentChild.ChildId,
                ParentUserId = _currentChild.ParentUserId,
                EncryptedFirstName = FirstName.Trim(),
                EncryptedLastName = LastName.Trim(),
                DateOfBirth = DateOfBirth,
                Weight = weight,
                Height = height,
                DiabetesType = DiabetesType,
                DiagnosisDate = DiagnosisDate,
                InsulinScheme = InsulinScheme.Trim(),
                CurrentInsulins = _currentChild.CurrentInsulins,
                CreatedAt = _currentChild.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                PhotoUrl = _currentChild.PhotoUrl
            };

            var validationResult = ProfileValidator.ValidateChildProfile(updatedChild);
            if (!validationResult.IsValid)
            {
                ValidationErrors = string.Join("\n", validationResult.Errors);
                return;
            }

            if (_isCreateMode)
            {
                await _childRepository.AddChildWithEncryptionAsync(updatedChild);
                await _storageService.SaveAsync(AppConstants.StorageKeyCurrentChildId, updatedChild.ChildId);
            }
            else
            {
                await _childRepository.UpdateChildWithEncryptionAsync(updatedChild);
            }

            await Shell.Current.DisplayAlert("Успех", "Профиль успешно обновлён", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (FormatException)
        {
            ValidationErrors = "Проверьте правильность введённых числовых значений";
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось сохранить профиль: {ex.Message}", "OK");
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

    // ── Фото ребёнка (TODO-3) ──────────────────────────────────────

    private static readonly HashSet<string> AllowedPhotoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };
    private const long MaxPhotoBytes = 5L * 1024L * 1024L; // 5 MB

    /// <summary>
    /// Открывает системный пикер фото, загружает выбранный файл на сервер,
    /// обновляет <see cref="PhotoUrl"/> и локальную БД.
    /// </summary>
    [RelayCommand]
    private async Task PickAndUploadPhotoAsync()
    {
        if (IsPhotoBusy) return;
        if (MediaPicker.Default.IsCaptureSupported is false)
        {
            // На устройствах без фото-пикера (например, desktop с тестами) — выходим тихо
        }

        try
        {
            var pickResult = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Выберите фото ребёнка"
            });
            if (pickResult is null)
                return; // пользователь отменил

            if (string.IsNullOrEmpty(pickResult.ContentType) ||
                !AllowedPhotoContentTypes.Contains(pickResult.ContentType))
            {
                await Shell.Current.DisplayAlert("Ошибка",
                    "Недопустимый формат. Разрешены: JPG, PNG, WebP, GIF.", "OK");
                return;
            }

            IsPhotoBusy = true;

            // Копируем в MemoryStream для HttpClient
            using var src = await pickResult.OpenReadAsync();
            using var buffer = new MemoryStream();
            await src.CopyToAsync(buffer);
            if (buffer.Length > MaxPhotoBytes)
            {
                await Shell.Current.DisplayAlert("Ошибка",
                    $"Файл слишком большой (макс. {MaxPhotoBytes / 1024 / 1024} МБ).", "OK");
                return;
            }
            buffer.Position = 0;

            var newPhotoUrl = await _apiClient.UploadChildPhotoAsync(
                _currentChild.ChildId, buffer, pickResult.FileName, pickResult.ContentType);

            if (string.IsNullOrEmpty(newPhotoUrl))
            {
                await Shell.Current.DisplayAlert("Ошибка",
                    "Не удалось загрузить фото на сервер.", "OK");
                return;
            }

            PhotoUrl = newPhotoUrl;
            _currentChild.PhotoUrl = newPhotoUrl;

            // Сохраняем PhotoUrl в локальной БД (через Update с минимальным набором полей)
            await _childRepository.UpdateChildWithEncryptionAsync(_currentChild);
        }
        catch (FeatureNotSupportedException)
        {
            await Shell.Current.DisplayAlert("Ошибка",
                "Выбор фото не поддерживается на этом устройстве.", "OK");
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlert("Ошибка",
                "Нет разрешения на доступ к фото.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка",
                $"Не удалось загрузить фото: {ex.Message}", "OK");
        }
        finally
        {
            IsPhotoBusy = false;
        }
    }

    /// <summary>
    /// Удаляет фото с сервера и очищает <see cref="PhotoUrl"/>.
    /// </summary>
    [RelayCommand]
    private async Task RemovePhotoAsync()
    {
        if (IsPhotoBusy || !HasPhoto) return;

        var confirm = await Shell.Current.DisplayAlert(
            "Удалить фото?",
            "Фото профиля будет удалено с сервера.",
            "Удалить",
            "Отмена");
        if (!confirm) return;

        try
        {
            IsPhotoBusy = true;

            var ok = await _apiClient.DeleteChildPhotoAsync(_currentChild.ChildId);
            if (!ok)
            {
                await Shell.Current.DisplayAlert("Ошибка",
                    "Не удалось удалить фото на сервере.", "OK");
                return;
            }

            PhotoUrl = null;
            _currentChild.PhotoUrl = null;
            await _childRepository.UpdateChildWithEncryptionAsync(_currentChild);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка",
                $"Не удалось удалить фото: {ex.Message}", "OK");
        }
        finally
        {
            IsPhotoBusy = false;
        }
    }

    private void CalculateBMI()
    {
        if (DoubleParser.TryParseDecrypted(Weight, out var w) && DoubleParser.TryParseDecrypted(Height, out var h))
        {
            CalculatedBMI = ProfileValidator.FieldValidators.CalculateBMI(w, h);
        }
        else
        {
            CalculatedBMI = 0;
        }
    }

    private void ValidateForm()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(FirstName))
            errors.Add("Введите имя");

        if (string.IsNullOrWhiteSpace(LastName))
            errors.Add("Введите фамилию");

        var (isValidDate, dateError) = ProfileValidator.FieldValidators.ValidateDateOfBirth(DateOfBirth);
        if (!isValidDate && dateError != null)
            errors.Add(dateError);

        if (DoubleParser.TryParseDecrypted(Weight, out var weight))
        {
            var (isValidWeight, weightError) = ProfileValidator.FieldValidators.ValidateWeight(weight);
            if (!isValidWeight && weightError != null)
                errors.Add(weightError);
        }
        else if (!string.IsNullOrEmpty(Weight))
        {
            errors.Add("Введите корректный вес");
        }

        if (DoubleParser.TryParseDecrypted(Height, out var height))
        {
            var (isValidHeight, heightError) = ProfileValidator.FieldValidators.ValidateHeight(height);
            if (!isValidHeight && heightError != null)
                errors.Add(heightError);
        }
        else if (!string.IsNullOrEmpty(Height))
        {
            errors.Add("Введите корректный рост");
        }

        ValidationErrors = errors.Count > 0 ? string.Join("\n", errors) : string.Empty;
    }
}
