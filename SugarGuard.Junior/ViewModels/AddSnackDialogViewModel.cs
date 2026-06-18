// ViewModel диалога добавления перекуса
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel диалога добавления перекуса в рюкзак
/// Отвечает за валидацию и сохранение нового перекуса
/// </summary>
public partial class AddSnackDialogViewModel : ObservableObject
{
    private readonly IBackpackService _backpackService;
    private readonly IBackpackRepository _backpackRepository;
    private readonly ILogger<AddSnackDialogViewModel> _logger;

    // ========== ПОЛЯ ВВОДА ==========

    /// <summary>
    /// Название перекуса
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormValid))]
    [NotifyPropertyChangedFor(nameof(SnackNameError))]
    private string snackName = string.Empty;

    /// <summary>
    /// Количество хлебных единиц (строка для ввода)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormValid))]
    [NotifyPropertyChangedFor(nameof(BreadUnitsError))]
    private string breadUnitsText = string.Empty;

    // ========== ОШИБКИ ВАЛИДАЦИИ ==========

    /// <summary>
    /// Ошибка валидации названия перекуса
    /// </summary>
    public string SnackNameError => ValidateSnackName();

    /// <summary>
    /// Ошибка валидации хлебных единиц
    /// </summary>
    public string BreadUnitsError => ValidateBreadUnits();

    /// <summary>
    /// Форма валидна и готова к отправке
    /// </summary>
    public bool IsFormValid => string.IsNullOrEmpty(SnackNameError) && 
                               string.IsNullOrEmpty(BreadUnitsError) &&
                               !string.IsNullOrWhiteSpace(SnackName) &&
                               !string.IsNullOrWhiteSpace(BreadUnitsText);


    // ========== СОСТОЯНИЕ ==========

    /// <summary>
    /// Индикатор загрузки
    /// </summary>
    [ObservableProperty]
    private bool isLoading = false;

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    [ObservableProperty]
    private string errorMessage = string.Empty;

    /// <summary>
    /// ID текущего ребёнка
    /// </summary>
    private string _currentChildId = string.Empty;

    /// <summary>
    /// Результат добавления перекуса (для возврата в вызывающий код)
    /// </summary>
    public BackpackItem? AddedSnack { get; private set; }

    /// <summary>
    /// Событие успешного добавления перекуса
    /// </summary>
    public event EventHandler<BackpackItem>? SnackAdded;

    /// <summary>
    /// Событие закрытия диалога
    /// </summary>
    public event EventHandler? DialogClosed;

    public AddSnackDialogViewModel(
        IBackpackService backpackService,
        IBackpackRepository backpackRepository,
        ILogger<AddSnackDialogViewModel> logger)
    {
        _backpackService = backpackService;
        _backpackRepository = backpackRepository;
        _logger = logger;
    }

    /// <summary>
    /// Инициализация диалога с ID ребёнка
    /// </summary>
    public void Initialize(string childId)
    {
        _currentChildId = childId;
        Reset();
        _logger.LogInformation("Диалог добавления перекуса инициализирован для: {ChildId}", childId);
    }

    /// <summary>
    /// Сброс формы в начальное состояние
    /// </summary>
    public void Reset()
    {
        SnackName = string.Empty;
        BreadUnitsText = string.Empty;
        ErrorMessage = string.Empty;
        IsLoading = false;
        AddedSnack = null;
    }

    // ========== КОМАНДЫ ==========

    /// <summary>
    /// Команда добавления перекуса
    /// </summary>
    [RelayCommand]
    public async Task AddSnackAsync()
    {
        try
        {
            // Проверяем валидность формы
            if (!IsFormValid)
            {
                _logger.LogWarning("Форма невалидна, добавление отменено");
                return;
            }

            IsLoading = true;
            ErrorMessage = string.Empty;

            _logger.LogInformation("Добавление перекуса: {SnackName}", SnackName);

            // Парсим хлебные единицы
            if (!DoubleParser.TryParseDecrypted(BreadUnitsText, out var breadUnits))
            {
                ErrorMessage = "Некорректное значение ХЕ";
                return;
            }

            // Добавляем перекус через сервис
            var result = await _backpackService.AddSnackAsync(
                _currentChildId, 
                SnackName.Trim(), 
                breadUnits);

            if (result != null)
            {
                AddedSnack = result;
                
                // Логируем с расшифрованными данными
                try
                {
                    var decryptedName = await _backpackRepository.GetDecryptedSnackNameAsync(result);
                    var decryptedBU = await _backpackRepository.GetDecryptedBreadUnitsAsync(result);
                    _logger.LogInformation(" Перекус добавлен: {SnackName} ({BreadUnits} ХЕ)", decryptedName, decryptedBU);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось дешифровать данные для логирования");
                    _logger.LogInformation(" Перекус добавлен");
                }
                
                // Уведомляем подписчиков
                SnackAdded?.Invoke(this, result);
                DialogClosed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = "Не удалось добавить перекус";
                _logger.LogWarning("Сервис вернул null при добавлении перекуса");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка: {ex.Message}";
            _logger.LogError(ex, "Ошибка при добавлении перекуса");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void SetPreset(string preset)
    {
        (SnackName, BreadUnitsText) = preset switch
        {
            "apple" => ("Яблоко", "1.0"),
            "juice" => ("Сок 200 мл", "2.0"),
            "cookie" => ("Печенье", "1.5"),
            "bar" => ("Батончик мюсли", "2.5"),
            _ => (SnackName, BreadUnitsText)
        };
    }

    /// <summary>
    /// Команда отмены и закрытия диалога
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        _logger.LogInformation("Добавление перекуса отменено");
        Reset();
        DialogClosed?.Invoke(this, EventArgs.Empty);
    }

    // ========== ВАЛИДАЦИЯ ==========

    /// <summary>
    /// Валидация названия перекуса
    /// </summary>
    private string ValidateSnackName()
    {
        if (string.IsNullOrWhiteSpace(SnackName))
        {
            return string.Empty; // Не показываем ошибку для пустого поля до первого ввода
        }

        var trimmed = SnackName.Trim();
        
        if (trimmed.Length < 2)
        {
            return "Минимум 2 символа";
        }

        if (trimmed.Length > 100)
        {
            return "Максимум 100 символов";
        }

        return string.Empty;
    }

    /// <summary>
    /// Валидация хлебных единиц
    /// </summary>
    private string ValidateBreadUnits()
    {
        if (string.IsNullOrWhiteSpace(BreadUnitsText))
        {
            return string.Empty; // Не показываем ошибку для пустого поля до первого ввода
        }

        // Пробуем распарсить значение
        if (!DoubleParser.TryParseDecrypted(BreadUnitsText, out var value))
        {
            return "Введите число";
        }

        if (value <= 0)
        {
            return "ХЕ должны быть > 0";
        }

        if (value > 50)
        {
            return "Максимум 50 ХЕ";
        }

        return string.Empty;
    }
}
