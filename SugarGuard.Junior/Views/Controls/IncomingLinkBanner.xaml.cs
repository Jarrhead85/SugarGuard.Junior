namespace SugarGuard.Junior.Views.Controls;

/// <summary>
/// Баннер входящего запроса на связку.
/// Показывается, когда родитель или врач запрашивает доступ к данным ребёнка.
/// </summary>
public partial class IncomingLinkBanner : ContentView
{
    public static readonly BindableProperty PersonNameProperty =
        BindableProperty.Create(nameof(PersonName), typeof(string), typeof(IncomingLinkBanner), string.Empty,
            propertyChanged: OnLabelChanged);

    public static readonly BindableProperty RoleProperty =
        BindableProperty.Create(nameof(Role), typeof(string), typeof(IncomingLinkBanner), string.Empty,
            propertyChanged: OnLabelChanged);

    public string PersonName
    {
        get => (string)GetValue(PersonNameProperty);
        set => SetValue(PersonNameProperty, value);
    }

    public string Role
    {
        get => (string)GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    public event EventHandler? Approved;
    public event EventHandler? Rejected;

    public IncomingLinkBanner()
    {
        InitializeComponent();
        UpdateMessage();
    }

    private static void OnLabelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is IncomingLinkBanner banner)
        {
            banner.UpdateMessage();
        }
    }

    private void UpdateMessage()
    {
        var roleText = Role switch
        {
            "Parent" => "как родитель",
            "Doctor" => "как врач",
            _ => ""
        };

        messageLabel.Text = $"{PersonName} хочет получить доступ к вашим данным {roleText}";
    }

    public void Show()
    {
        IsVisible = true;
    }

    public void Hide()
    {
        IsVisible = false;
    }

    private void OnApproveClicked(object? sender, EventArgs e)
    {
        Approved?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    private void OnRejectClicked(object? sender, EventArgs e)
    {
        Rejected?.Invoke(this, EventArgs.Empty);
        Hide();
    }
}
