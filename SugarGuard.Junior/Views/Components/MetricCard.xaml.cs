namespace SugarGuard.Junior.Views.Components;

public partial class MetricCard : ContentView
{
    // Заголовок метрики, например
    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(
            nameof(Label),
            typeof(string),
            typeof(MetricCard),
            string.Empty);

    // Основное значение метрики
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(
            nameof(Value),
            typeof(string),
            typeof(MetricCard),
            string.Empty,
            propertyChanged: OnValueChanged);

    // Единица измерения, например
    public static readonly BindableProperty UnitProperty =
        BindableProperty.Create(
            nameof(Unit),
            typeof(string),
            typeof(MetricCard),
            string.Empty,
            propertyChanged: OnUnitChanged);

    // Нижняя helper-подпись
    public static readonly BindableProperty HelperTextProperty =
        BindableProperty.Create(
            nameof(HelperText),
            typeof(string),
            typeof(MetricCard),
            string.Empty,
            propertyChanged: OnHelperTextChanged);

    // Маленький badge в правом верхнем углу
    public static readonly BindableProperty BadgeTextProperty =
        BindableProperty.Create(
            nameof(BadgeText),
            typeof(string),
            typeof(MetricCard),
            string.Empty,
            propertyChanged: OnBadgeTextChanged);

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public string HelperText
    {
        get => (string)GetValue(HelperTextProperty);
        set => SetValue(HelperTextProperty, value);
    }

    public string BadgeText
    {
        get => (string)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    // Безопасное отображение значения, чтобы карточка не ломалась
    public string ResolvedValue =>
        string.IsNullOrWhiteSpace(Value) ? "—" : Value;

    // Показываем единицу измерения только если она задана
    public bool HasUnit =>
        !string.IsNullOrWhiteSpace(Unit);

    // Показываем helper-подпись только если она задана
    public bool HasHelperText =>
        !string.IsNullOrWhiteSpace(HelperText);

    // Показываем badge только если он задан
    public bool HasBadge =>
        !string.IsNullOrWhiteSpace(BadgeText);

    public MetricCard()
    {
        InitializeComponent();
    }

    // При смене значения обновляем вычисляемое отображение
    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MetricCard card)
        {
            card.OnPropertyChanged(nameof(ResolvedValue));
        }
    }

    // При смене Unit обновляем видимость единицы измерения
    private static void OnUnitChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MetricCard card)
        {
            card.OnPropertyChanged(nameof(HasUnit));
        }
    }

    // При смене helper-подписи обновляем ее видимость
    private static void OnHelperTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MetricCard card)
        {
            card.OnPropertyChanged(nameof(HasHelperText));
        }
    }

    // При смене badge обновляем его видимость
    private static void OnBadgeTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MetricCard card)
        {
            card.OnPropertyChanged(nameof(HasBadge));
        }
    }
}
