namespace SugarGuard.Junior.Views.Modals;

/// <summary>
/// Bottom Sheet, показываемый при попытке использовать заблокированную
/// функцию до верификации email.
/// </summary>
public partial class LockedFeatureBottomSheet : ContentView
{
    public static readonly BindableProperty FeatureNameProperty =
        BindableProperty.Create(nameof(FeatureName), typeof(string), typeof(LockedFeatureBottomSheet), string.Empty,
            propertyChanged: OnTextChanged);

    public string FeatureName
    {
        get => (string)GetValue(FeatureNameProperty);
        set => SetValue(FeatureNameProperty, value);
    }

    public event EventHandler? VerifyTapped;

    public LockedFeatureBottomSheet()
    {
        InitializeComponent();
    }

    private static void OnTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is LockedFeatureBottomSheet sheet && newValue is string featureName)
        {
            sheet.titleLabel.Text = $"{featureName} недоступен";
            sheet.descriptionLabel.Text = $"{featureName} будет доступен после подтверждения email";
        }
    }

    public void Show(string featureName)
    {
        FeatureName = featureName;
        IsVisible = true;
        this.FadeTo(1, 280, Easing.CubicOut);
    }

    public void Hide()
    {
        this.FadeTo(0, 200, Easing.CubicIn);
        IsVisible = false;
    }

    private async void OnVerifyClicked(object? sender, EventArgs e)
    {
        VerifyTapped?.Invoke(this, EventArgs.Empty);
        Hide();
        await Shell.Current.GoToAsync("//verifypage");
    }

    private void OnCloseClicked(object? sender, EventArgs e)
    {
        Hide();
    }

    private void OnScrimTapped(object? sender, TappedEventArgs e)
    {
        Hide();
    }
}
