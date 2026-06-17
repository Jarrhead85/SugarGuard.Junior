namespace SugarGuard.Junior.Views.Controls;

/// <summary>
/// Баннер ограниченного режима, показывается пока email не верифицирован.
/// Направляет пользователя на экран верификации.
/// </summary>
public partial class LimitedModeBanner : ContentView
{
    public static readonly BindableProperty IsEmailVerifiedProperty =
        BindableProperty.Create(nameof(IsEmailVerified), typeof(bool), typeof(LimitedModeBanner),
            true, propertyChanged: OnVisibilityChanged);

    public bool IsEmailVerified
    {
        get => (bool)GetValue(IsEmailVerifiedProperty);
        set => SetValue(IsEmailVerifiedProperty, value);
    }

    public event EventHandler? VerifyTapped;

    public LimitedModeBanner()
    {
        InitializeComponent();
    }

    public void Show()
    {
        IsVisible = true;
    }

    public void Hide()
    {
        IsVisible = false;
    }

    private static void OnVisibilityChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is LimitedModeBanner banner)
        {
            banner.IsVisible = !(bool)newValue;
        }
    }

    private async void OnBannerTapped(object? sender, TappedEventArgs e)
    {
        VerifyTapped?.Invoke(this, EventArgs.Empty);
        await Shell.Current.GoToAsync("//verifypage");
    }
}
