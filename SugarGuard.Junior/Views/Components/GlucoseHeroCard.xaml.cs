using SugarGuard.Domain.Enums;

namespace SugarGuard.Junior.Views.Components;

public partial class GlucoseHeroCard : ContentView
{
    // Значение сахара, которое показываем крупным числом
    public static readonly BindableProperty GlucoseValueProperty =
        BindableProperty.Create(
            nameof(GlucoseValue),
            typeof(decimal),
            typeof(GlucoseHeroCard),
            0m);

    // Состояние сахара: Normal / Attention / Critical
    public static readonly BindableProperty UiStateProperty =
        BindableProperty.Create(
            nameof(UiState),
            typeof(GlucoseUiState),
            typeof(GlucoseHeroCard),
            GlucoseUiState.Normal,
            propertyChanged: OnUiStateChanged);

    // Время последнего измерения, например: "2 мин назад"
    public static readonly BindableProperty TimeSinceMeasurementProperty =
        BindableProperty.Create(
            nameof(TimeSinceMeasurement),
            typeof(string),
            typeof(GlucoseHeroCard),
            string.Empty,
            propertyChanged: OnTimeChanged);

    // Необязательная пользовательская подсказка
    public static readonly BindableProperty HelperTextProperty =
        BindableProperty.Create(
            nameof(HelperText),
            typeof(string),
            typeof(GlucoseHeroCard),
            string.Empty,
            propertyChanged: OnHelperChanged);

    public decimal GlucoseValue
    {
        get => (decimal)GetValue(GlucoseValueProperty);
        set => SetValue(GlucoseValueProperty, value);
    }

    public GlucoseUiState UiState
    {
        get => (GlucoseUiState)GetValue(UiStateProperty);
        set => SetValue(UiStateProperty, value);
    }

    public string TimeSinceMeasurement
    {
        get => (string)GetValue(TimeSinceMeasurementProperty);
        set => SetValue(TimeSinceMeasurementProperty, value);
    }

    public string HelperText
    {
        get => (string)GetValue(HelperTextProperty);
        set => SetValue(HelperTextProperty, value);
    }

    // Безопасный текст времени, чтобы в hero не было пустой строки
    public string ResolvedTimeText =>
        string.IsNullOrWhiteSpace(TimeSinceMeasurement)
            ? "Нет свежих данных"
            : TimeSinceMeasurement;

    // Если helper не передали снаружи, показываем дефолтную подсказку по состоянию
    public string ResolvedHelperText =>
        !string.IsNullOrWhiteSpace(HelperText)
            ? HelperText
            : UiState switch
            {
                GlucoseUiState.Normal => "Все спокойно. Значение в безопасной зоне.",
                GlucoseUiState.Attention => "Нужно внимание. Проверь самочувствие и недавнюю еду или активность.",
                GlucoseUiState.Critical => "Нужна помощь взрослого. Следуй сценарию помощи без задержки.",
                _ => "Следим за значением и состоянием."
            };

    private GlucoseChartDrawable? _chartDrawable;

    public GlucoseChartDrawable? ChartDrawable
    {
        get => _chartDrawable;
        set => _chartDrawable = value;
    }

    public GlucoseHeroCard()
    {
        InitializeComponent();
    }

    // При смене состояния обновляем helper-подсказку
    private static void OnUiStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GlucoseHeroCard card)
        {
            card.OnPropertyChanged(nameof(ResolvedHelperText));
        }
    }

    // При смене времени обновляем строку времени
    private static void OnTimeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GlucoseHeroCard card)
        {
            card.OnPropertyChanged(nameof(ResolvedTimeText));
        }
    }

    // При смене helper-текста обновляем итоговую подсказку
    private static void OnHelperChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GlucoseHeroCard card)
        {
            card.OnPropertyChanged(nameof(ResolvedHelperText));
        }
    }
}
