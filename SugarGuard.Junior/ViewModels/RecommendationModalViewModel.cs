using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Services.Interfaces;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// Перекус с расшифрованным названием для отображения в UI.
/// Используется только внутри модального окна рекомендации.
/// </summary>
public class DecryptedSnackItem
{
    /// <summary>Идентификатор записи в рюкзаке.</summary>
    public string BackpackItemId { get; set; } = string.Empty;

    /// <summary>Расшифрованное название перекуса.</summary>
    public string SnackName { get; set; } = string.Empty;

    /// <summary>Хлебные единицы.</summary>
    public double BreadUnits { get; set; }

    /// <summary>Время добавления в рюкзак (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// ViewModel модального окна рекомендации.
/// Отвечает за отображение совета от ИИ и выбор перекуса из рюкзака.
/// </summary>
public partial class RecommendationModalViewModel : ObservableObject
{
    private readonly IBackpackService _backpackService;
    private readonly IMeasurementService _measurementService;
    private readonly IBackpackRepository _backpackRepository;
    private readonly IStorageService _storageService;
    private readonly ILogger<RecommendationModalViewModel> _logger;

    // ── Наблюдаемые свойства ───────────────────────────────────────

    /// <summary>Текущая рекомендация, полученная от ИИ или локального движка.</summary>
    [ObservableProperty]
    private RecommendationResponse? currentRecommendation = null;

    /// <summary>Текст рекомендации для отображения пользователю.</summary>
    [ObservableProperty]
    private string recommendationText = string.Empty;

    /// <summary>Локализованная метка уровня срочности: «В НОРМЕ», «ВНИМАНИЕ», «КРИТИЧНО».</summary>
    [ObservableProperty]
    private string recommendationUrgency = "Информация";

    /// <summary>
    /// Семантический ключ цвета для уровня срочности.
    /// Значения: "Primary" / "Warning" / "Danger" / "TextMuted".
    /// XAML использует конвертер StatusToColorConverter для получения Color из Resources.
    /// Заменяет прежний UrgencyColorHex (хардкоженный hex).
    /// </summary>
    [ObservableProperty]
    private string urgencyColorKey = "Primary";

    /// <summary>Список перекусов из рюкзака с расшифрованными названиями.</summary>
    [ObservableProperty]
    private List<DecryptedSnackItem> availableSnacks = [];

    /// <summary>True — рекомендация взята из кэша, а не от ИИ.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecommendationSourceTitle))]
    private bool isFromCache = false;

    /// <summary>Текст метки источника рекомендации.</summary>
    [ObservableProperty]
    private string cacheLabel = string.Empty;

    // ── Вычисляемые свойства ───────────────────────────────────────

    /// <summary>
    /// Заголовок модалки: «Совет от ИИ» или «Рекомендация приложения».
    /// Зависит от флага IsFromCache.
    /// </summary>
    public string RecommendationSourceTitle =>
        IsFromCache ? "Рекомендация приложения" : "Совет от ИИ";

    // ── Приватное состояние ────────────────────────────────────────

    /// <summary>ID текущего ребёнка — нужен для загрузки рюкзака.</summary>
    private string _currentChildId = string.Empty;

    // ── Команды ───────────────────────────────────────────────────

    /// <summary>Выбрать перекус: логирует потребление и закрывает модалку.</summary>
    public IAsyncRelayCommand<string?> SelectSnackCommand { get; }

    /// <summary>Закрыть модальное окно без действий.</summary>
    public IAsyncRelayCommand CloseModalCommand { get; }

    /// <summary>Пропустить рекомендацию: логирует пропуск и закрывает модалку.</summary>
    public IAsyncRelayCommand SkipRecommendationCommand { get; }

    // ── Конструктор ───────────────────────────────────────────────

    public RecommendationModalViewModel(
        IBackpackService backpackService,
        IMeasurementService measurementService,
        IBackpackRepository backpackRepository,
        IStorageService storageService,
        ILogger<RecommendationModalViewModel> logger)
    {
        _backpackService = backpackService;
        _measurementService = measurementService;
        _backpackRepository = backpackRepository;
        _storageService = storageService;
        _logger = logger;

        SelectSnackCommand = new AsyncRelayCommand<string?>(OnSelectSnackAsync);
        CloseModalCommand = new AsyncRelayCommand(OnCloseModalAsync);
        SkipRecommendationCommand = new AsyncRelayCommand(OnSkipRecommendationAsync);
    }

    // ── Публичные методы ──────────────────────────────────────────

    /// <summary>
    /// Устанавливает рекомендацию для отображения.
    /// Вызывается из MainPageViewModel после получения рекомендации.
    /// </summary>
    /// <param name="recommendation">Ответ от ИИ или локального движка.</param>
    public async Task SetRecommendationAsync(RecommendationResponse recommendation)
    {
        try
        {
            _logger.LogInformation("Установка рекомендации в ViewModel");

            // GetAsync принимает string и возвращает Task<string?> — без generic-параметра.
            _currentChildId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId)
                              ?? string.Empty;

            if (string.IsNullOrEmpty(_currentChildId))
            {
                _logger.LogWarning("ChildId не найден при открытии рекомендации");
                RecommendationText = "Ребёнок не выбран. Перейдите в профиль.";
                return;
            }

            CurrentRecommendation = recommendation;
            RecommendationText = recommendation.RecommendationText
                                 ?? recommendation.ActionText
                                 ?? "Рекомендация недоступна";

            // Определяем метку срочности и семантический ключ цвета.
            // UrgencyColorKey используется через StatusToColorConverter в XAML.
            (RecommendationUrgency, UrgencyColorKey) = recommendation.Urgency switch
            {
                "critical" => ("КРИТИЧНО!", "Danger"),
                "warning" => ("ВНИМАНИЕ", "Warning"),
                "normal" => ("В НОРМЕ", "Primary"),
                _ => ("Информация", "TextMuted")
            };

            // Источник рекомендации
            IsFromCache = recommendation.IsFromCache;
            CacheLabel = IsFromCache
                ? "Приложение (на основе сохранённых данных)"
                : "ИИ сделал предложение";

            // Загружаем список перекусов из рюкзака
            await LoadAvailableSnacksAsync();

            _logger.LogInformation("Рекомендация установлена успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при установке рекомендации");
        }
    }

    // ── Приватные методы ──────────────────────────────────────────

    /// <summary>
    /// Загружает перекусы из рюкзака и дешифрует их названия для отображения.
    /// </summary>
    private async Task LoadAvailableSnacksAsync()
    {
        try
        {
            _logger.LogInformation("Загрузка перекусов из рюкзака");

            var snacks = await _backpackService.GetBackpackAsync(_currentChildId);
            var decryptedSnacks = new List<DecryptedSnackItem>();

            foreach (var snack in snacks)
            {
                try
                {
                    var decryptedName = await _backpackRepository.GetDecryptedSnackNameAsync(snack);
                    var decryptedBreadUnits = await _backpackRepository.GetDecryptedBreadUnitsAsync(snack);

                    decryptedSnacks.Add(new DecryptedSnackItem
                    {
                        BackpackItemId = snack.BackpackItemId,
                        SnackName = decryptedName,
                        BreadUnits = decryptedBreadUnits,
                        CreatedAt = snack.CreatedAt
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Не удалось дешифровать название перекуса {ItemId}",
                        snack.BackpackItemId);
                }
            }

            // Сортируем по названию для удобства выбора
            AvailableSnacks = decryptedSnacks.OrderBy(s => s.SnackName).ToList();

            _logger.LogInformation("Загружено {Count} перекусов", decryptedSnacks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке перекусов из рюкзака");
            AvailableSnacks = [];
        }
    }

    /// <summary>
    /// Обрабатывает выбор перекуса пользователем.
    /// Логирует потребление через MeasurementService и закрывает модалку.
    /// </summary>
    /// <param name="snackItemId">BackpackItemId выбранного перекуса.</param>
    private async Task OnSelectSnackAsync(string? snackItemId)
    {
        try
        {
            if (string.IsNullOrEmpty(snackItemId))
            {
                _logger.LogWarning("Перекус не выбран — snackItemId пустой");
                return;
            }

            var selectedSnack = AvailableSnacks.FirstOrDefault(s => s.BackpackItemId == snackItemId);
            if (selectedSnack is null)
            {
                _logger.LogWarning("Перекус не найден в рюкзаке: {SnackId}", snackItemId);
                return;
            }

            _logger.LogInformation(
                "Выбран перекус: {SnackName} ({BreadUnits} ХЕ)",
                selectedSnack.SnackName,
                selectedSnack.BreadUnits);

            var consumedRequest = new SnackConsumedRequest
            {
                ChildId = _currentChildId,
                BackpackItemId = selectedSnack.BackpackItemId,
                SnackName = selectedSnack.SnackName,
                BreadUnits = selectedSnack.BreadUnits,
                RecommendationId = CurrentRecommendation?.RecommendationId ?? string.Empty,
                ConsumedAt = DateTime.UtcNow
            };

            var success = await _measurementService.LogSnackConsumedAsync(consumedRequest);

            if (success)
            {
                _logger.LogInformation("Перекус успешно зарегистрирован");

                // Удаляем из локального списка — UI обновится мгновенно
                AvailableSnacks = AvailableSnacks
                    .Where(s => s.BackpackItemId != snackItemId)
                    .ToList();

                await OnCloseModalAsync();
            }
            else
            {
                _logger.LogError("Не удалось зарегистрировать потребление перекуса");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выборе перекуса");
        }
    }

    /// <summary>
    /// Пропускает рекомендацию: логирует действие пользователя и закрывает модалку.
    /// </summary>
    private async Task OnSkipRecommendationAsync()
    {
        try
        {
            _logger.LogInformation("Пользователь пропустил рекомендацию");

            if (CurrentRecommendation is not null)
            {
                var skippedRequest = new SkippedRecommendationRequest
                {
                    ChildId = _currentChildId,
                    RecommendationId = CurrentRecommendation.RecommendationId,
                    Reason = "user_skipped",
                    SkippedAt = DateTime.UtcNow
                };

                await _measurementService.LogSkippedRecommendationAsync(skippedRequest);
            }

            await OnCloseModalAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при пропуске рекомендации");
        }
    }

    /// <summary>
    /// Закрывает модальное окно и очищает состояние ViewModel.
    /// </summary>
    private async Task OnCloseModalAsync()
    {
        try
        {
            _logger.LogInformation("Закрытие модального окна рекомендации");

            // Очищаем состояние перед закрытием
            CurrentRecommendation = null;
            RecommendationText = string.Empty;
            AvailableSnacks = [];
            IsFromCache = false;
            CacheLabel = string.Empty;

            // Закрываем верхнее модальное окно через Navigation API
            if (Application.Current?.Windows is { Count: > 0 } windows)
            {
                var navigation = windows[0]?.Page?.Navigation;
                if (navigation is not null)
                    await navigation.PopModalAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при закрытии модального окна");
        }
    }
}
