using System.Windows.Input;
using Microsoft.Maui.Graphics;

namespace SugarGuard.Junior.Views.Components;

public partial class SyncBanner : ContentView
{
    // Поддерживаемые состояния баннера синхронизации
    public const string PendingState = "Pending";
    public const string SuccessState = "Success";
    public const string ConflictState = "Conflict";
    public const string FailedState = "Failed";

    // Текущее состояние синхронизации
    public static readonly BindableProperty SyncStateProperty =
        BindableProperty.Create(
            nameof(SyncState),
            typeof(string),
            typeof(SyncBanner),
            PendingState,
            propertyChanged: OnBannerPropertyChanged);

    // Количество элементов в очереди синхронизации
    public static readonly BindableProperty PendingCountProperty =
        BindableProperty.Create(
            nameof(PendingCount),
            typeof(int),
            typeof(SyncBanner),
            0,
            propertyChanged: OnBannerPropertyChanged);

    // Пользовательское сообщение.
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(
            nameof(Message),
            typeof(string),
            typeof(SyncBanner),
            string.Empty,
            propertyChanged: OnBannerPropertyChanged);

    // Видимость баннера
    public static readonly BindableProperty IsBannerVisibleProperty =
        BindableProperty.Create(
            nameof(IsBannerVisible),
            typeof(bool),
            typeof(SyncBanner),
            true,
            propertyChanged: OnBannerPropertyChanged);

    // Можно ли скрыть баннер вручную
    public static readonly BindableProperty IsDismissibleProperty =
        BindableProperty.Create(
            nameof(IsDismissible),
            typeof(bool),
            typeof(SyncBanner),
            true,
            propertyChanged: OnBannerPropertyChanged);

    // Текст дополнительного действия, например "Повторить"
    public static readonly BindableProperty ActionTextProperty =
        BindableProperty.Create(
            nameof(ActionText),
            typeof(string),
            typeof(SyncBanner),
            string.Empty,
            propertyChanged: OnBannerPropertyChanged);

    // Команда дополнительного действия
    public static readonly BindableProperty ActionCommandProperty =
        BindableProperty.Create(
            nameof(ActionCommand),
            typeof(ICommand),
            typeof(SyncBanner),
            null,
            propertyChanged: OnBannerPropertyChanged);

    public string SyncState
    {
        get => (string)GetValue(SyncStateProperty);
        set => SetValue(SyncStateProperty, value);
    }

    public int PendingCount
    {
        get => (int)GetValue(PendingCountProperty);
        set => SetValue(PendingCountProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IsBannerVisible
    {
        get => (bool)GetValue(IsBannerVisibleProperty);
        set => SetValue(IsBannerVisibleProperty, value);
    }

    public bool IsDismissible
    {
        get => (bool)GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    // Команда закрытия баннера
    public Command DismissCommand { get; }

    public SyncBanner()
    {
        InitializeComponent();

        // Команда закрытия по умолчанию
        DismissCommand = new Command(() => IsBannerVisible = false);

        // Привязываем кнопку закрытия один раз
        CloseButton.Command = DismissCommand;

        // Инициализируем внешний вид
        UpdateVisualState();
    }

    // Общий обработчик изменений bindable-свойств
    private static void OnBannerPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SyncBanner banner)
        {
            banner.UpdateVisualState();
        }
    }

    // Полное обновление визуального состояния баннера
    private void UpdateVisualState()
    {
        IsVisible = IsBannerVisible;
        CloseButton.IsVisible = IsDismissible;

        var normalizedState = NormalizeState(SyncState);
        var accentColor = GetAccentColor(normalizedState);

        BannerBorder.BackgroundColor = GetBackgroundColor(normalizedState);
        BannerBorder.Stroke = GetStrokeColor(normalizedState);

        IconLabel.Text = GetBannerIcon(normalizedState);
        IconLabel.TextColor = accentColor;

        TitleLabel.Text = GetBannerTitle(normalizedState);
        MessageLabel.Text = GetBannerMessage(normalizedState);

        ActionButton.Text = ActionText ?? string.Empty;
        ActionButton.Command = ActionCommand;
        ActionButton.TextColor = accentColor;
        ActionButton.IsVisible = !string.IsNullOrWhiteSpace(ActionText) && ActionCommand is not null;
    }

    // Нормализуем состояние, чтобы компонент не ломался на пустой строке
    private static string NormalizeState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return PendingState;
        }

        return state.Trim();
    }

    // Заголовок баннера по состоянию
    private static string GetBannerTitle(string state) => state switch
    {
        SuccessState => "Синхронизация завершена",
        ConflictState => "Нужна проверка синхронизации",
        FailedState => "Синхронизация не удалась",
        _ => "Есть локальные изменения"
    };

    // Основной текст баннера
    private string GetBannerMessage(string state)
    {
        if (!string.IsNullOrWhiteSpace(Message))
        {
            return Message;
        }

        return state switch
        {
            SuccessState => "Все локальные изменения успешно отправлены на сервер.",
            ConflictState => "Обнаружен конфликт данных. Проверь запись и выбери актуальную версию.",
            FailedState => "Нет соединения или сервер временно недоступен. Данные сохранены локально.",
            _ => PendingCount > 0
                ? $"В очереди синхронизации: {PendingCount}."
                : "Изменения сохранены локально и будут отправлены позже."
        };
    }

    // Иконка состояния
    private static string GetBannerIcon(string state) => state switch
    {
        SuccessState => "",
        ConflictState => "",
        FailedState => "⛔",
        _ => "↻"
    };

    // Акцентный цвет состояния
    private static Color GetAccentColor(string state) => state switch
    {
        SuccessState => Color.FromArgb("#37A563"),
        ConflictState => Color.FromArgb("#E3A32B"),
        FailedState => Color.FromArgb("#DB5967"),
        _ => Color.FromArgb("#1B8E8B")
    };

    // Фон баннера
    private static Color GetBackgroundColor(string state) => state switch
    {
        SuccessState => Color.FromArgb("#1A37A563"),
        ConflictState => Color.FromArgb("#1AE3A32B"),
        FailedState => Color.FromArgb("#1ADB5967"),
        _ => Color.FromArgb("#1A1B8E8B")
    };

    // Рамка баннера
    private static Color GetStrokeColor(string state) => state switch
    {
        SuccessState => Color.FromArgb("#4D37A563"),
        ConflictState => Color.FromArgb("#4DE3A32B"),
        FailedState => Color.FromArgb("#4DDB5967"),
        _ => Color.FromArgb("#4D1B8E8B")
    };
}
