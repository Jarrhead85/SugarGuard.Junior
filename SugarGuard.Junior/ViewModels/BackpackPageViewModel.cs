using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.ViewModels;
using System.Collections.ObjectModel;
using System.Globalization;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Страницы «Мой рюкзак».
/// </summary>
public partial class BackpackPageViewModel : ObservableObject
{
    // Зависимости
    private readonly IBackpackService _backpackService;
    private readonly IBackpackRepository _backpackRepository;
    private readonly IAddSnackDialogFactory _addSnackDialogFactory;
    private readonly ICurrentUserService _currentUserService; 
    private readonly ILogger<BackpackPageViewModel> _logger;
    private readonly IStorageService _storageService;

    // ID ребёнка, чей рюкзак сейчас открыт
    private string _currentChildId = string.Empty;

    public BackpackPageViewModel(
        IBackpackService backpackService,
        IBackpackRepository backpackRepository,
        IAddSnackDialogFactory addSnackDialogFactory,
        ICurrentUserService currentUserService,
        ILogger<BackpackPageViewModel> logger,
        IStorageService storageService)
    {
        _backpackService = backpackService;
        _backpackRepository = backpackRepository;
        _addSnackDialogFactory = addSnackDialogFactory;
        _currentUserService = currentUserService;
        _logger = logger;
        _storageService = storageService;
    }

    // Коллекции
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowContentState))]
    [NotifyPropertyChangedFor(nameof(ShowStatisticsCard))]
    private ObservableCollection<BackpackItemViewModel> backpackItems = new();

    //Есть ли хотя бы один перекус в рюкзаке.
    public bool HasItems => BackpackItems.Count > 0;

    //Показывать ли пустое состояние (нет перекусов, нет ошибки, не грузим).
    public bool ShowEmptyState => !IsLoading && !HasError && IsBackpackEmpty;

    //Показывать ли список перекусов.
    public bool ShowContentState => !IsLoading && !HasError && HasItems;

    //Показывать ли карточку статистики ХЕ.
    public bool ShowStatisticsCard => HasSelectedChild && !HasError && (HasItems || ConsumedToday > 0);

    // Данные рюкзака
    [ObservableProperty]
    private double totalBreadUnits;

    [ObservableProperty]
    private string totalBreadUnitsText = "0.0 ХЕ";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowContentState))]
    private bool isBackpackEmpty = true;

    [ObservableProperty]
    private int snackCount;

    // Статистика хлебных единиц за сегодня
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStatisticsCard))]
    private double consumedToday;

    [ObservableProperty]
    private double remainingBreadUnits;

    [ObservableProperty]
    private double totalPerDay;

    // Состояния UI
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddSnackCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConsumeSnackCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSnackCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryLoadCommand))]
    [NotifyPropertyChangedFor(nameof(CanShowQuickActions))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowContentState))]
    [NotifyPropertyChangedFor(nameof(ShowErrorState))]
    [NotifyPropertyChangedFor(nameof(CanShowQuickActions))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddSnackCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConsumeSnackCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSnackCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryLoadCommand))]
    [NotifyPropertyChangedFor(nameof(CanShowQuickActions))]
    private bool isDialogOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowContentState))]
    [NotifyPropertyChangedFor(nameof(ShowErrorState))]
    private bool hasError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddSnackCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConsumeSnackCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSnackCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryLoadCommand))]
    [NotifyPropertyChangedFor(nameof(CanShowQuickActions))]
    [NotifyPropertyChangedFor(nameof(ShowStatisticsCard))]
    private bool hasSelectedChild;

    [ObservableProperty]
    private string errorTitle = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    //Показывать ли экран ошибки.
    public bool ShowErrorState => !IsLoading && HasError;

    //Доступны ли быстрые действия (добавить / удалить).
    public bool CanShowQuickActions => HasSelectedChild && !IsLoading && !IsBusy;

    // Надписи (hero card, empty state, quick actions)
    [ObservableProperty]
    private string heroTitle = "Мой рюкзак";

    [ObservableProperty]
    private string heroSubtitle = "Здесь лежат перекусы на день";

    [ObservableProperty]
    private string heroBadgeText = "Готов";

    [ObservableProperty]
    private string summaryText = "Следи за тем, чтобы в рюкзаке всегда были быстрые перекусы.";

    [ObservableProperty]
    private string statisticsSummaryText = "Статистика рюкзака появится после загрузки данных.";

    [ObservableProperty]
    private string primaryActionText = "Добавить перекус";

    [ObservableProperty]
    private string secondaryActionText = "Обновить";

    [ObservableProperty]
    private string quickActionsHintText = "Быстрые действия помогают быстро пополнить рюкзак.";

    // Тексты пустого состояния
    [ObservableProperty]
    private string emptyStateIcon = "";

    [ObservableProperty]
    private string emptyStateTitle = "Рюкзак пока пуст";

    [ObservableProperty]
    private string emptyStateMessage = "Добавь первый перекус, и здесь появятся карточки со списком и статистикой.";

    [ObservableProperty]
    private string emptyStatePrimaryActionText = "Добавить перекус";

    [ObservableProperty]
    private string emptyStateSecondaryActionText = "Обновить";

    // Инициализация
    public async Task InitializeAsync()
    {
        if (IsBusy)
            return;

        var childId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);

        if (string.IsNullOrWhiteSpace(childId))
        {
            _logger.LogInformation("Текущий ребёнок не выбран — рюкзак не загружен");
            ApplyNoChildState();
            return;
        }

        await InitializeAsync(childId);
    }

    /// <summary>
    /// Инициализация страницы для конкретного ребёнка.
    /// </summary>
    public async Task InitializeAsync(string childId)
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(childId))
        {
            ApplyNoChildState();
            return;
        }

        try
        {
            _logger.LogInformation("Инициализация BackpackPage для ребёнка {ChildId}", childId);

            IsBusy = true;
            IsLoading = true;
            ClearErrorState();

            _currentChildId = childId;
            HasSelectedChild = true;

            await ReloadPageDataAsync();

            _logger.LogInformation("Инициализация BackpackPage завершена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации BackpackPage");

            ApplyErrorState(
                "Не удалось открыть рюкзак",
                "Попробуй обновить экран ещё раз. Данные рюкзака останутся в приложении.");

            await DisplayAlert("Ошибка", ErrorMessage, "ОК");
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
            UpdatePresentationState();
        }
    }

    // Команды
    private bool CanAddSnack() => HasSelectedChild && !IsDialogOpen && !IsBusy;

    /// <summary>
    /// Открывает модальный диалог добавления перекуса в рюкзак.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddSnack))]
    public async Task AddSnack()
    {
        if (!HasSelectedChild || string.IsNullOrWhiteSpace(_currentChildId))
        {
            await DisplayAlert(
                "Сначала выбери ребёнка",
                "Когда будет выбран профиль ребёнка, можно будет добавить перекус в рюкзак.",
                "ОК");
            return;
        }

        var currentPage = GetCurrentPage();
        if (currentPage is null)
        {
            _logger.LogWarning("Не удалось открыть диалог добавления перекуса: текущая страница не найдена");
            return;
        }

        IsDialogOpen = true;

        try
        {
            _logger.LogInformation("Открытие диалога добавления перекуса");

            var (dialog, viewModel) = _addSnackDialogFactory.Create();

            viewModel.SnackAdded += OnSnackAdded;

            // Снимаем подписку и сбрасываем флаг при закрытии диалога
            void OnDialogDisappearing(object? sender, EventArgs args)
            {
                if (sender is ContentPage page)
                    page.Disappearing -= OnDialogDisappearing;

                viewModel.SnackAdded -= OnSnackAdded;
                IsDialogOpen = false;
            }

            dialog.Disappearing += OnDialogDisappearing;
            dialog.Initialize(viewModel, _currentChildId);

            await currentPage.Navigation.PushModalAsync(dialog);

            _logger.LogInformation("Диалог добавления перекуса открыт");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось открыть диалог добавления перекуса");
            IsDialogOpen = false;
            await DisplayAlert("Ошибка", $"Не удалось открыть диалог: {ex.Message}", "ОК");
        }
    }

    private bool CanRemoveSnack(string backpackItemId) =>
        HasSelectedChild &&
        !IsBusy &&
        !IsDialogOpen &&
        !string.IsNullOrWhiteSpace(backpackItemId);

    /// <summary>
    /// Удаляет перекус из рюкзака после подтверждения пользователем.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemoveSnack))]
    public async Task RemoveSnack(string backpackItemId)
    {
        if (string.IsNullOrWhiteSpace(backpackItemId))
            return;

        var confirmed = await DisplayConfirmAsync(
            "Удалить перекус",
            "Этот перекус исчезнет из рюкзака. Продолжить?",
            "Удалить",
            "Отмена");

        if (!confirmed)
            return;

        try
        {
            IsBusy = true;
            ClearErrorState();

            _logger.LogInformation("Удаление перекуса {BackpackItemId}", backpackItemId);

            // Получаем ID пользователя, выполняющего удаление
            var actorId = await _currentUserService.GetCurrentUserIdAsync()
                          ?? AppConstants.AuditActorUnknown;

            var removed = await _backpackService.RemoveSnackAsync(
                backpackItemId,
                _currentChildId,
                actorId); 

            if (!removed)
            {
                _logger.LogWarning("Перекус {BackpackItemId} не был удалён", backpackItemId);

                await DisplayAlert(
                    "Не удалось удалить",
                    "Перекус не найден или уже был удалён ранее.",
                    "ОК");

                return;
            }

            await ReloadPageDataAsync();

            _logger.LogInformation("Перекус {BackpackItemId} успешно удалён", backpackItemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении перекуса {BackpackItemId}", backpackItemId);

            ApplyErrorState(
                "Не удалось удалить перекус",
                "Попробуй ещё раз чуть позже. Рюкзак останется в безопасном состоянии.");

            await DisplayAlert("Ошибка", $"Не удалось удалить перекус: {ex.Message}", "ОК");
        }
        finally
        {
            IsBusy = false;
            UpdatePresentationState();
        }
    }

    private bool CanConsumeSnack(BackpackItemViewModel? item) =>
        item is not null && HasSelectedChild && !IsBusy && !IsDialogOpen;

    [RelayCommand(CanExecute = nameof(CanConsumeSnack))]
    public async Task ConsumeSnack(BackpackItemViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(_currentChildId))
            return;

        var confirmed = await DisplayConfirmAsync(
            "Съесть перекус",
            $"Отметить «{item.SnackName}» ({item.BreadUnits:F1} ХЕ) как съеденный?",
            "Съесть",
            "Отмена");

        if (!confirmed)
            return;

        try
        {
            IsBusy = true;
            ClearErrorState();

            var glucoseText = await _storageService.GetAsync(AppConstants.StorageKeyLastGlucoseValue);
            var currentGlucose = double.TryParse(
                glucoseText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsedGlucose)
                ? parsedGlucose
                : 0d;

            var consumed = await _backpackService.ConsumeSnackAsync(
                item.BackpackItemId,
                _currentChildId,
                item.SnackName,
                item.BreadUnits,
                currentGlucose);

            if (!consumed)
            {
                await DisplayAlert("Не удалось отметить перекус", "Обнови рюкзак и попробуй ещё раз.", "ОК");
                return;
            }

            if (item.BackpackItemIds.Count > 0)
            {
                item.BackpackItemIds.Remove(item.BackpackItemId);
            }

            if (item.Quantity > 1 && item.BackpackItemIds.Count > 0)
            {
                item.Quantity--;
                item.BackpackItemId = item.BackpackItemIds[0];
                BackpackItems = new ObservableCollection<BackpackItemViewModel>(BackpackItems);
            }
            else
            {
                BackpackItems.Remove(item);
            }

            SnackCount = BackpackItems.Sum(entry => entry.Quantity);
            IsBackpackEmpty = SnackCount == 0;
            TotalBreadUnits = BackpackItems.Sum(entry => entry.TotalBreadUnits);
            TotalBreadUnitsText = $"{TotalBreadUnits:F1} ХЕ";
            await LoadStatisticsAsync();
            await DisplayAlert("Готово", $"{item.SnackName} отмечен как съеденный.", "ОК");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при учёте съеденного перекуса {BackpackItemId}", item.BackpackItemId);
            await DisplayAlert("Не удалось сохранить", "Проверь подключение и попробуй ещё раз.", "ОК");
        }
        finally
        {
            IsBusy = false;
            UpdatePresentationState();
        }
    }

    private bool CanRefresh() => !IsBusy && !IsDialogOpen;

    /// <summary>
    /// Обновляет содержимое рюкзака и статистику.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task Refresh()
    {
        // Если ребёнок ещё не выбран — пробуем сначала определить его из хранилища
        if (!HasSelectedChild)
        {
            await InitializeAsync();
            return;
        }

        try
        {
            IsBusy = true;
            IsLoading = true;
            ClearErrorState();

            _logger.LogInformation("Ручное обновление рюкзака");

            await ReloadPageDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении рюкзака");

            ApplyErrorState(
                "Не удалось обновить рюкзак",
                "Проверь подключение и попробуй ещё раз.");

            await DisplayAlert("Ошибка", ErrorMessage, "ОК");
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
            UpdatePresentationState();
        }
    }

    /// <summary>
    /// Повторная попытка загрузки после ошибки
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RetryLoad()
    {
        await Refresh();
    }

    // Обработчики событий

    /// <summary>
    /// Вызывается диалогом после успешного добавления перекуса. Обновляет список и статистику.
    /// </summary>
    private async void OnSnackAdded(object? sender, BackpackItem item)
    {
        try
        {
            var decryptedName = await _backpackRepository.GetDecryptedSnackNameAsync(item);
            _logger.LogInformation("Перекус добавлен через диалог: {SnackName}", decryptedName);

            // Отписываемся сразу после первого срабатывания
            if (sender is AddSnackDialogViewModel viewModel)
                viewModel.SnackAdded -= OnSnackAdded;

            await ReloadPageDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении рюкзака после добавления перекуса");

            ApplyErrorState(
                "Данные обновились не полностью",
                "Перекус был добавлен, но список не успел обновиться. Нажми «Обновить».");

            UpdatePresentationState();
        }
    }

    // Загрузка данных
    /// <summary>
    /// Полная перезагрузка экрана: список перекусов + статистика ХЕ.
    /// </summary>
    private async Task ReloadPageDataAsync()
    {
        await LoadBackpackAsync();
        await LoadStatisticsAsync();
        UpdatePresentationState();
    }

    /// <summary>
    /// Загружает список перекусов из сервиса, расшифровывает и сортирует.
    /// </summary>
    private async Task LoadBackpackAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentChildId))
        {
            ApplyNoChildState();
            return;
        }

        _logger.LogInformation("Загрузка содержимого рюкзака для ребёнка {ChildId}", _currentChildId);

        var items = await _backpackService.GetBackpackAsync(_currentChildId);
        items ??= new List<BackpackItem>();

        // Расшифровываем все элементы параллельно для скорости
        var mapTasks = items.Select(MapBackpackItemAsync);
        var mappedItems = (await Task.WhenAll(mapTasks))
            .Where(x => x is not null)
            .Cast<BackpackItemViewModel>()
            .GroupBy(
                x => $"{NormalizeSnackName(x.SnackName)}|{x.BreadUnits:F3}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                first.BackpackItemIds = group.Select(item => item.BackpackItemId).ToList();
                first.BackpackItemId = first.BackpackItemIds[0];
                first.Quantity = first.BackpackItemIds.Count;
                return first;
            })
            // Сначала перекусы с бо́льшим числом ХЕ, затем по алфавиту
            .OrderByDescending(x => x.TotalBreadUnits)
            .ThenBy(x => x.SnackName)
            .ToList();

        BackpackItems = new ObservableCollection<BackpackItemViewModel>(mappedItems);

        IsBackpackEmpty = BackpackItems.Count == 0;
        SnackCount = BackpackItems.Sum(item => item.Quantity);

        TotalBreadUnits = BackpackItems.Sum(x => x.TotalBreadUnits);
        TotalBreadUnitsText = $"{TotalBreadUnits:F1} ХЕ";

        _logger.LogInformation("Загружено {Count} перекусов", BackpackItems.Count);
    }

    /// <summary>
    /// Загружает статистику ХЕ за сегодня: сколько съедено, сколько осталось.
    /// </summary>
    private async Task LoadStatisticsAsync()
    {
        _logger.LogInformation("Загрузка статистики рюкзака");

        if (string.IsNullOrWhiteSpace(_currentChildId))
        {
            ConsumedToday = 0;
            RemainingBreadUnits = 0;
            TotalPerDay = 0;
            return;
        }

        var consumed = await _backpackRepository.GetConsumedBreadUnitsTodayAsync(_currentChildId);

        // Защита от некорректных отрицательных значений из хранилища
        ConsumedToday = Math.Max(0, consumed);

        // Остаток
        RemainingBreadUnits = Math.Max(0, TotalBreadUnits);

        // Итого за день = уже съедено + запас в рюкзаке
        TotalPerDay = ConsumedToday + RemainingBreadUnits;

        _logger.LogInformation(
            "Статистика: съедено {ConsumedToday:F1} ХЕ, в рюкзаке {Remaining:F1} ХЕ",
            ConsumedToday,
            RemainingBreadUnits);
    }

    /// <summary>
    /// Преобразует сущность БД в карточку.
    /// </summary>
    private async Task<BackpackItemViewModel?> MapBackpackItemAsync(BackpackItem item)
    {
        try
        {
            var decryptedName = await _backpackRepository.GetDecryptedSnackNameAsync(item);
            var decryptedBreadUnits = await _backpackRepository.GetDecryptedBreadUnitsAsync(item);

            return new BackpackItemViewModel
            {
                BackpackItemId = item.BackpackItemId,
                BackpackItemIds = [item.BackpackItemId],
                SnackName = decryptedName,
                BreadUnits = decryptedBreadUnits,
                Quantity = 1,
                SnackIcon = GetSnackIcon(decryptedName)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не удалось расшифровать данные перекуса {ItemId}",
                item.BackpackItemId);

            return null;
        }
    }

    private static string NormalizeSnackName(string value) =>
        string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    // Презентационный слой

    /// <summary>
    /// Обновляет тексты hero-карточки, empty state и quick actions
    /// </summary>
    private void UpdatePresentationState()
    {
        PrimaryActionText = "Добавить перекус";
        SecondaryActionText = "Обновить";

        if (!HasSelectedChild)
        {
            HeroTitle = "Мой рюкзак";
            HeroSubtitle = "Сначала выбери профиль ребёнка";
            HeroBadgeText = "Нет профиля";

            SummaryText = "Когда профиль будет выбран, здесь появятся карточки, список перекусов и статистика.";
            StatisticsSummaryText = "Статистика станет доступна после выбора ребёнка.";

            EmptyStateIcon = "�";
            EmptyStateTitle = "Сначала выбери ребёнка";
            EmptyStateMessage = "После выбора профиля рюкзак загрузится автоматически.";
            EmptyStatePrimaryActionText = "Обновить";
            EmptyStateSecondaryActionText = "Пока скрыть";

            QuickActionsHintText = "Сначала нужен активный профиль ребёнка.";
            return;
        }

        if (HasError)
        {
            HeroTitle = "Мой рюкзак";
            HeroSubtitle = "Не удалось загрузить данные";
            HeroBadgeText = "Ошибка";

            SummaryText = string.IsNullOrWhiteSpace(ErrorMessage)
                ? "Что-то пошло не так. Попробуй обновить экран."
                : ErrorMessage;

            StatisticsSummaryText = "После повторной загрузки статистика появится снова.";

            EmptyStateIcon = "";
            EmptyStateTitle = string.IsNullOrWhiteSpace(ErrorTitle) ? "Ошибка загрузки" : ErrorTitle;
            EmptyStateMessage = string.IsNullOrWhiteSpace(ErrorMessage)
                ? "Не удалось получить данные рюкзака."
                : ErrorMessage;
            EmptyStatePrimaryActionText = "Попробовать снова";
            EmptyStateSecondaryActionText = "Обновить";

            QuickActionsHintText = "Если ошибка повторяется, проверь подключение к сети.";
            return;
        }

        if (IsBackpackEmpty)
        {
            HeroTitle = "Мой рюкзак";
            HeroSubtitle = "Пока здесь нет перекусов";
            HeroBadgeText = "Пусто";

            SummaryText = "Добавь первый перекус — и на экране появятся карточки со списком и полезной статистикой.";
            StatisticsSummaryText = ConsumedToday > 0
                ? $"Сегодня уже съедено {ConsumedToday:F1} ХЕ. Можно добавить новые перекусы в запас."
                : "Когда в рюкзаке появятся перекусы, здесь будет проще следить за запасом.";

            EmptyStateIcon = "";
            EmptyStateTitle = "Рюкзак пока пуст";
            EmptyStateMessage = "Добавь первый перекус, чтобы собрать удобный список и быстро видеть остаток ХЕ.";
            EmptyStatePrimaryActionText = "Добавить перекус";
            EmptyStateSecondaryActionText = "Обновить";

            QuickActionsHintText = "Начни с одного-двух быстрых перекусов на случай низкого сахара.";
            return;
        }

        // Рюкзак заполнен — нормальное состояние
        HeroTitle = "Мой рюкзак";
        HeroSubtitle = BuildHeroSubtitle();
        HeroBadgeText = RemainingBreadUnits <= 1.0 ? "Пора пополнить" : "Готов";

        SummaryText = BuildSummaryText();
        StatisticsSummaryText = BuildStatisticsSummaryText();

        EmptyStateIcon = "";
        EmptyStateTitle = "Рюкзак пока пуст";
        EmptyStateMessage = "Добавь первый перекус, чтобы собрать удобный список и быстро видеть остаток ХЕ.";
        EmptyStatePrimaryActionText = "Добавить перекус";
        EmptyStateSecondaryActionText = "Обновить";

        QuickActionsHintText = RemainingBreadUnits <= 1.0
            ? "Запас заканчивается — можно быстро добавить ещё один перекус."
            : "Быстрые действия помогут держать рюкзак аккуратным и полезным.";
    }

    /// <summary>
    /// Сбрасывает всё состояние к начальному: ребёнок не выбран, рюкзак пуст.
    /// </summary>
    private void ApplyNoChildState()
    {
        _currentChildId = string.Empty;
        HasSelectedChild = false;

        BackpackItems = new ObservableCollection<BackpackItemViewModel>();
        IsBackpackEmpty = true;

        TotalBreadUnits = 0;
        TotalBreadUnitsText = "0.0 ХЕ";
        SnackCount = 0;
        ConsumedToday = 0;
        RemainingBreadUnits = 0;
        TotalPerDay = 0;

        ClearErrorState();
        UpdatePresentationState();
    }

    // Переводит UI в состояние ошибки с заголовком и сообщением.
    private void ApplyErrorState(string title, string message)
    {
        HasError = true;
        ErrorTitle = title;
        ErrorMessage = message;
    }

    // Сбрасывает состояние ошибки.
    private void ClearErrorState()
    {
        HasError = false;
        ErrorTitle = string.Empty;
        ErrorMessage = string.Empty;
    }

    // Вспомогательные методы для построения текстов

    private string BuildHeroSubtitle()
    {
        return SnackCount switch
        {
            0 => "Пока здесь нет перекусов",
            1 => "В рюкзаке 1 перекус",
            _ => $"В рюкзаке {SnackCount} перекуса"
        };
    }

    private string BuildSummaryText()
    {
        if (RemainingBreadUnits <= 0)
            return "Запас закончился — можно добавить новый перекус, чтобы рюкзак снова был готов.";

        if (RemainingBreadUnits <= 1.0)
            return $"В рюкзаке осталось всего {RemainingBreadUnits:F1} ХЕ. Лучше пополнить запас заранее.";

        return $"Сейчас в рюкзаке {RemainingBreadUnits:F1} ХЕ. Этого достаточно для быстрого перекуса при необходимости.";
    }

    private string BuildStatisticsSummaryText()
    {
        if (ConsumedToday <= 0)
            return $"Сегодня в рюкзаке лежит {RemainingBreadUnits:F1} ХЕ. Пока ничего не было съедено.";

        return $"Сегодня уже съедено {ConsumedToday:F1} ХЕ, а в рюкзаке осталось {RemainingBreadUnits:F1} ХЕ.";
    }

    // Утилиты

    /// <summary>
    /// Подбирает иконку по названию перекуса для отображения в карточке.
    /// </summary>
    private static string GetSnackIcon(string snackName)
    {
        var lower = snackName.ToLowerInvariant();

        return lower switch
        {
            _ when lower.Contains("яблоко") => "�",
            _ when lower.Contains("груша") => "�",
            _ when lower.Contains("банан") => "�",
            _ when lower.Contains("апельсин") => "�",
            _ when lower.Contains("сок") => "�",
            _ when lower.Contains("хлеб") => "�",
            _ when lower.Contains("печенье") => "�",
            _ when lower.Contains("бутерброд") => "�",
            _ when lower.Contains("конфета") => "�",
            _ when lower.Contains("конфет") => "�",
            _ when lower.Contains("сахар") => "�",
            _ when lower.Contains("молоко") => "�",
            _ when lower.Contains("йогурт") => "�",
            _ => "�"
        };
    }

    /// <summary>
    /// Возвращает активную страницу приложения для показа алертов и навигации.
    /// </summary>
    private static Page? GetCurrentPage()
    {
        return Application.Current?.Windows.FirstOrDefault()?.Page
               ?? Shell.Current?.CurrentPage;
    }

    /// <summary>
    /// Показывает системный алерт с одной кнопкой.
    /// </summary>
    private static Task DisplayAlert(string title, string message, string ok)
    {
        var page = GetCurrentPage();
        return page?.DisplayAlert(title, message, ok) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Показывает диалог подтверждения и возвращает true, если пользователь согласился.
    /// </summary>
    private static Task<bool> DisplayConfirmAsync(string title, string message, string accept, string cancel)
    {
        var page = GetCurrentPage();
        return page?.DisplayAlert(title, message, accept, cancel) ?? Task.FromResult(false);
    }
}
