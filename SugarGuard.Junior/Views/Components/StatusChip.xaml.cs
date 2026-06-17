using SugarGuard.Domain.Enums;

namespace SugarGuard.Junior.Views.Components;

public partial class StatusChip : ContentView
{
    // Основное bindable-свойство состояния чипа
    public static readonly BindableProperty UiStateProperty =
        BindableProperty.Create(
            nameof(UiState),
            typeof(GlucoseUiState),
            typeof(StatusChip),
            GlucoseUiState.Normal,
            propertyChanged: OnUiStateChanged);

    // Состояние чипа: Normal / Attention / Critical
    public GlucoseUiState UiState
    {
        get => (GlucoseUiState)GetValue(UiStateProperty);
        set => SetValue(UiStateProperty, value);
    }

    // Текстовая подпись состояния.
    public string StatusText => UiState switch
    {
        GlucoseUiState.Normal => "Норма",
        GlucoseUiState.Attention => "Внимание",
        GlucoseUiState.Critical => "Критично",
        _ => "Норма"
    };

    public StatusChip()
    {
        InitializeComponent();
    }

    // При смене состояния обновляем текст
    private static void OnUiStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StatusChip chip)
        {
            chip.OnPropertyChanged(nameof(StatusText));
        }
    }
}
