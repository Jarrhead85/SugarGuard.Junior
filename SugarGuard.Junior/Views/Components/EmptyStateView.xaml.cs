using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace SugarGuard.Junior.Views.Components;

public partial class EmptyStateView : ContentView
{
    // Доступные тона для helper-блока и акцентов empty-state
    public const string NeutralTone = "Neutral";
    public const string InfoTone = "Info";
    public const string WarmTone = "Warm";
    public const string SuccessTone = "Success";

    // Основная иконка состояния
    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(
            nameof(Icon),
            typeof(string),
            typeof(EmptyStateView),
            "�",
            propertyChanged: OnAnyPropertyChanged);

    // Заголовок empty-state
    public static readonly BindableProperty TitleTextProperty =
        BindableProperty.Create(
            nameof(TitleText),
            typeof(string),
            typeof(EmptyStateView),
            "Пока пусто",
            propertyChanged: OnAnyPropertyChanged);

    // Основное сообщение
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(
            nameof(Message),
            typeof(string),
            typeof(EmptyStateView),
            "Здесь пока нет данных.",
            propertyChanged: OnAnyPropertyChanged);

    // Дополнительный helper-текст с более мягким тоном
    public static readonly BindableProperty HelperTextProperty =
        BindableProperty.Create(
            nameof(HelperText),
            typeof(string),
            typeof(EmptyStateView),
            "Когда появятся первые данные, мы покажем их здесь.",
            propertyChanged: OnAnyPropertyChanged);

    // Тон empty-state: Neutral / Info / Warm / Success
    public static readonly BindableProperty HelperToneProperty =
        BindableProperty.Create(
            nameof(HelperTone),
            typeof(string),
            typeof(EmptyStateView),
            NeutralTone,
            propertyChanged: OnAnyPropertyChanged);

    // Нужно ли показывать помощника
    public static readonly BindableProperty IsMascotVisibleProperty =
        BindableProperty.Create(
            nameof(IsMascotVisible),
            typeof(bool),
            typeof(EmptyStateView),
            true,
            propertyChanged: OnAnyPropertyChanged);

    // Символ помощника
    public static readonly BindableProperty MascotProperty =
        BindableProperty.Create(
            nameof(Mascot),
            typeof(string),
            typeof(EmptyStateView),
            "•ᴗ•",
            propertyChanged: OnAnyPropertyChanged);

    // Текст помощника
    public static readonly BindableProperty MascotTextProperty =
        BindableProperty.Create(
            nameof(MascotText),
            typeof(string),
            typeof(EmptyStateView),
            "Я помогу, когда здесь появятся первые записи.",
            propertyChanged: OnAnyPropertyChanged);

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string HelperText
    {
        get => (string)GetValue(HelperTextProperty);
        set => SetValue(HelperTextProperty, value);
    }

    public string HelperTone
    {
        get => (string)GetValue(HelperToneProperty);
        set => SetValue(HelperToneProperty, value);
    }

    public bool IsMascotVisible
    {
        get => (bool)GetValue(IsMascotVisibleProperty);
        set => SetValue(IsMascotVisibleProperty, value);
    }

    public string Mascot
    {
        get => (string)GetValue(MascotProperty);
        set => SetValue(MascotProperty, value);
    }

    public string MascotText
    {
        get => (string)GetValue(MascotTextProperty);
        set => SetValue(MascotTextProperty, value);
    }

    public EmptyStateView()
    {
        InitializeComponent();

        // Подписываемся на смену темы
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        }

        UpdateVisualState();
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        // Отписываемся при уничтожении handler
        if (args.NewHandler is null && Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        }

        base.OnHandlerChanging(args);
    }

    private static void OnAnyPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is EmptyStateView view)
        {
            view.UpdateVisualState();
        }
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        UpdateVisualState();
    }

    // Полностью обновляем внешний вид компонента
    private void UpdateVisualState()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var tone = NormalizeTone(HelperTone);

        var accent = GetAccentColor(isDark, tone);
        var softAccent = GetSoftAccentColor(isDark, tone);
        var cardBackground = GetCardBackgroundColor(isDark);
        var cardStroke = GetCardStrokeColor(isDark);
        var helperColor = GetHelperTextColor(isDark, tone);
        var mascotBackground = GetMascotBackgroundColor(isDark, tone);
        var mascotStroke = GetMascotStrokeColor(isDark, tone);
        var mascotTextColor = GetMascotTextColor(isDark);

        CardBorder.BackgroundColor = cardBackground;
        CardBorder.Stroke = cardStroke;

        IconContainer.BackgroundColor = softAccent;
        IconContainer.Stroke = accent;

        IconLabel.Text = string.IsNullOrWhiteSpace(Icon) ? "�" : Icon;
        IconLabel.TextColor = accent;

        TitleLabel.Text = string.IsNullOrWhiteSpace(TitleText)
            ? "Пока пусто"
            : TitleText;

        MessageLabel.Text = string.IsNullOrWhiteSpace(Message)
            ? "Здесь пока нет данных."
            : Message;

        HelperLabel.Text = string.IsNullOrWhiteSpace(HelperText)
            ? "Когда появятся первые данные, мы покажем их здесь."
            : HelperText;
        HelperLabel.TextColor = helperColor;

        var shouldShowMascot =
            IsMascotVisible &&
            !string.IsNullOrWhiteSpace(MascotText);

        MascotBubble.IsVisible = shouldShowMascot;

        if (shouldShowMascot)
        {
            MascotBubble.BackgroundColor = mascotBackground;
            MascotBubble.Stroke = mascotStroke;

            MascotLabel.Text = string.IsNullOrWhiteSpace(Mascot) ? "•ᴗ•" : Mascot;
            MascotLabel.TextColor = accent;

            MascotTextLabel.Text = MascotText;
            MascotTextLabel.TextColor = mascotTextColor;
        }
    }

    // Нормализуем тон, чтобы компонент не ломался на неожиданных строках
    private static string NormalizeTone(string? tone)
    {
        if (string.IsNullOrWhiteSpace(tone))
        {
            return NeutralTone;
        }

        return tone.Trim() switch
        {
            InfoTone => InfoTone,
            WarmTone => WarmTone,
            SuccessTone => SuccessTone,
            _ => NeutralTone
        };
    }

    // Основной акцент empty-state
    private static Color GetAccentColor(bool isDark, string tone) => tone switch
    {
        InfoTone => Color.FromArgb(isDark ? "#6DAEFF" : "#2678D9"),
        WarmTone => Color.FromArgb(isDark ? "#F4BC56" : "#E3A32B"),
        SuccessTone => Color.FromArgb(isDark ? "#62D889" : "#37A563"),
        _ => Color.FromArgb(isDark ? "#56D0BF" : "#1B8E8B")
    };

    // Мягкий фон для иконки
    private static Color GetSoftAccentColor(bool isDark, string tone) => tone switch
    {
        InfoTone => Color.FromArgb(isDark ? "#1A6DAEFF" : "#142678D9"),
        WarmTone => Color.FromArgb(isDark ? "#1AF4BC56" : "#14E3A32B"),
        SuccessTone => Color.FromArgb(isDark ? "#1A62D889" : "#1437A563"),
        _ => Color.FromArgb(isDark ? "#1A56D0BF" : "#141B8E8B")
    };

    // Фон карточки empty-state
    private static Color GetCardBackgroundColor(bool isDark) =>
        Color.FromArgb(isDark ? "#EB111825" : "#F2FFFFFF");

    // Рамка карточки
    private static Color GetCardStrokeColor(bool isDark) =>
        Color.FromArgb(isDark ? "#17EDF4FF" : "#1A16213E");

    // Цвет helper-текста
    private static Color GetHelperTextColor(bool isDark, string tone) => tone switch
    {
        InfoTone => Color.FromArgb(isDark ? "#8BBEFF" : "#336FC4"),
        WarmTone => Color.FromArgb(isDark ? "#FFD07A" : "#B57B16"),
        SuccessTone => Color.FromArgb(isDark ? "#8BE7AE" : "#2E8A52"),
        _ => Color.FromArgb(isDark ? "#9EAED0" : "#667694")
    };

    // Фон bubble для помощника
    private static Color GetMascotBackgroundColor(bool isDark, string tone) => tone switch
    {
        InfoTone => Color.FromArgb(isDark ? "#14203B57" : "#F1F6FF"),
        WarmTone => Color.FromArgb(isDark ? "#143C2D12" : "#FFF8EA"),
        SuccessTone => Color.FromArgb(isDark ? "#14213325" : "#EEF9F2"),
        _ => Color.FromArgb(isDark ? "#141B2635" : "#F5F8FC")
    };

    // Рамка bubble для помощника
    private static Color GetMascotStrokeColor(bool isDark, string tone) => tone switch
    {
        InfoTone => Color.FromArgb(isDark ? "#246DAEFF" : "#1A2678D9"),
        WarmTone => Color.FromArgb(isDark ? "#24F4BC56" : "#1AE3A32B"),
        SuccessTone => Color.FromArgb(isDark ? "#2462D889" : "#1A37A563"),
        _ => Color.FromArgb(isDark ? "#20D5E4FF" : "#1416213E")
    };

    // Цвет текста в bubble
    private static Color GetMascotTextColor(bool isDark) =>
        Color.FromArgb(isDark ? "#EDF4FF" : "#16213E");
}
