namespace SugarGuard.Junior.Views.Modals;

/// <summary>
/// Bottom Sheet для отображения кода приглашения.
/// Показывает код, кнопку «Поделиться» и таймер действия.
/// </summary>
public partial class InviteCodeBottomSheet : ContentView
{
    public static readonly BindableProperty InviteCodeProperty =
        BindableProperty.Create(nameof(InviteCode), typeof(string), typeof(InviteCodeBottomSheet), string.Empty);

    public string InviteCode
    {
        get => (string)GetValue(InviteCodeProperty);
        set => SetValue(InviteCodeProperty, value);
    }

    public InviteCodeBottomSheet()
    {
        InitializeComponent();
    }

    public void Show()
    {
        IsVisible = true;
        this.FadeTo(1, 280, Easing.CubicOut);
        this.TranslateTo(0, 0, 280, Easing.CubicOut);
    }

    public void Hide()
    {
        this.FadeTo(0, 200, Easing.CubicIn);
        IsVisible = false;
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(InviteCode))
        {
            await Clipboard.Default.SetTextAsync(InviteCode);
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = $"Приглашение в SugarGuard: {InviteCode}",
                Title = "Приглашение родителя"
            });
        }
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
