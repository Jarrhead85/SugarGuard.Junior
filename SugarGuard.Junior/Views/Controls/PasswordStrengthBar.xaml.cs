using System.Text.RegularExpressions;

namespace SugarGuard.Junior.Views.Controls;

/// <summary>
/// Индикатор сложности пароля из 3 сегментов.
/// Слабый (красный) → Средний (жёлтый) → Сильный (зелёный).
/// </summary>
public partial class PasswordStrengthBar : ContentView
{
    public static readonly BindableProperty PasswordProperty =
        BindableProperty.Create(nameof(Password), typeof(string), typeof(PasswordStrengthBar),
            string.Empty, propertyChanged: OnPasswordChanged);

    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public PasswordStrengthBar()
    {
        InitializeComponent();
    }

    private static void OnPasswordChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PasswordStrengthBar bar)
        {
            bar.UpdateStrength(newValue as string ?? string.Empty);
        }
    }

    /// <summary>Обновляет отображение индикатора на основе сложности пароля.</summary>
    private void UpdateStrength(string password)
    {
        var level = CalculateStrength(password);

        var weakColor = Application.Current!.Resources["GlucoseDangerColor"] is Color wc ? wc : Colors.Red;
        var mediumColor = Application.Current.Resources["GlucoseWarningColor"] is Color mc ? mc : Colors.Orange;
        var strongColor = Application.Current.Resources["GlucoseNormalColor"] is Color sc ? sc : Colors.Green;
        var inactiveColor = Application.Current.Resources["SurfaceOffset"] is Color ic ? ic : Colors.LightGray;

        segment1.BackgroundColor = inactiveColor;
        segment2.BackgroundColor = inactiveColor;
        segment3.BackgroundColor = inactiveColor;

        switch (level)
        {
            case PasswordStrength.Weak:
                segment1.BackgroundColor = weakColor;
                strengthLabel.Text = "Слабый";
                strengthLabel.TextColor = weakColor;
                break;

            case PasswordStrength.Medium:
                segment1.BackgroundColor = mediumColor;
                segment2.BackgroundColor = mediumColor;
                strengthLabel.Text = "Средний";
                strengthLabel.TextColor = mediumColor;
                break;

            case PasswordStrength.Strong:
                segment1.BackgroundColor = strongColor;
                segment2.BackgroundColor = strongColor;
                segment3.BackgroundColor = strongColor;
                strengthLabel.Text = "Сильный";
                strengthLabel.TextColor = strongColor;
                break;

            default:
                strengthLabel.Text = string.Empty;
                break;
        }
    }

    /// <summary>Оценивает сложность пароля.</summary>
    private static PasswordStrength CalculateStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return PasswordStrength.Empty;
        }

        int score = 0;

        // Длина
        if (password.Length >= 8)
        {
            score++;
        }
        if (password.Length >= 12)
        {
            score++;
        }

        // Строчные буквы
        if (Regex.IsMatch(password, "[a-zа-я]"))
        {
            score++;
        }

        // Заглавные буквы
        if (Regex.IsMatch(password, "[A-ZА-Я]"))
        {
            score++;
        }

        // Цифры
        if (Regex.IsMatch(password, "[0-9]"))
        {
            score++;
        }

        // Спецсимволы
        if (Regex.IsMatch(password, "[^a-zA-Zа-яА-Я0-9]"))
        {
            score++;
        }

        if (score <= 2)
        {
            return PasswordStrength.Weak;
        }

        if (score <= 4)
        {
            return PasswordStrength.Medium;
        }

        return PasswordStrength.Strong;
    }

    private enum PasswordStrength
    {
        Empty,
        Weak,
        Medium,
        Strong
    }
}
