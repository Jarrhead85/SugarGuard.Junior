namespace SugarGuard.Junior.Views.Controls;

/// <summary>
/// Двойной ползунок для выбора целевого диапазона глюкозы (мин–макс).
/// Значения округляются до 0.1 ммоль/л.
/// </summary>
public partial class RangeSlider : ContentView
{
    private bool _isUpdating;

    public static readonly BindableProperty MinGlucoseProperty =
        BindableProperty.Create(nameof(MinGlucose), typeof(double), typeof(RangeSlider),
            4.0, BindingMode.TwoWay, propertyChanged: OnMinGlucoseChanged);

    public static readonly BindableProperty MaxGlucoseProperty =
        BindableProperty.Create(nameof(MaxGlucose), typeof(double), typeof(RangeSlider),
            10.0, BindingMode.TwoWay, propertyChanged: OnMaxGlucoseChanged);

    public double MinGlucose
    {
        get => (double)GetValue(MinGlucoseProperty);
        set => SetValue(MinGlucoseProperty, Math.Round(value, 1));
    }

    public double MaxGlucose
    {
        get => (double)GetValue(MaxGlucoseProperty);
        set => SetValue(MaxGlucoseProperty, Math.Round(value, 1));
    }

    public RangeSlider()
    {
        InitializeComponent();
        UpdateLabels();
        UpdateActiveTrack();
    }

    private void OnMinSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;

        var rounded = Math.Round(e.NewValue, 1);

        if (rounded >= maxSlider.Value - 0.5)
        {
            minSlider.Value = maxSlider.Value - 0.5;
        }
        else
        {
            minSlider.Value = rounded;
        }

        MinGlucose = Math.Round(minSlider.Value, 1);
        UpdateLabels();
        UpdateActiveTrack();

        _isUpdating = false;
    }

    private void OnMaxSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;

        var rounded = Math.Round(e.NewValue, 1);

        if (rounded <= minSlider.Value + 0.5)
        {
            maxSlider.Value = minSlider.Value + 0.5;
        }
        else
        {
            maxSlider.Value = rounded;
        }

        MaxGlucose = Math.Round(maxSlider.Value, 1);
        UpdateLabels();
        UpdateActiveTrack();

        _isUpdating = false;
    }

    private void UpdateLabels()
    {
        minValueLabel.Text = $"{MinGlucose:F1} ммоль/л";
        maxValueLabel.Text = $"{MaxGlucose:F1} ммоль/л";
    }

    private void UpdateActiveTrack()
    {
        var totalRange = maxSlider.Maximum - maxSlider.Minimum;
        if (totalRange <= 0)
        {
            return;
        }

        var minPercent = (minSlider.Value - maxSlider.Minimum) / totalRange;
        var maxPercent = (maxSlider.Value - maxSlider.Minimum) / totalRange;

        activeTrack.Margin = new Thickness(minPercent * 320, 0, (1 - maxPercent) * 320, 0);
    }

    private static void OnMinGlucoseChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is RangeSlider slider && !slider._isUpdating)
        {
            slider._isUpdating = true;
            slider.minSlider.Value = (double)newValue;
            slider.UpdateLabels();
            slider.UpdateActiveTrack();
            slider._isUpdating = false;
        }
    }

    private static void OnMaxGlucoseChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is RangeSlider slider && !slider._isUpdating)
        {
            slider._isUpdating = true;
            slider.maxSlider.Value = (double)newValue;
            slider.UpdateLabels();
            slider.UpdateActiveTrack();
            slider._isUpdating = false;
        }
    }
}
