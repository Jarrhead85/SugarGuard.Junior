using Microsoft.Maui.Controls.Shapes;

using SugarGuard.Shared.Constants;

namespace SugarGuard.Junior.Views.Controls;

/// <summary>
/// Compact 8-symbol code input for ABCD-1234 verification codes.
/// </summary>
public partial class CodeInputControl : ContentView
{
    private const int CodeLength = ConnectionCodeFormat.Length;

    private readonly Entry[] _boxes = new Entry[CodeLength];
    private bool _isShaking;
    private bool _isUpdatingText;

    public static readonly BindableProperty CodeProperty =
        BindableProperty.Create(nameof(Code), typeof(string), typeof(CodeInputControl),
            string.Empty, BindingMode.TwoWay, propertyChanged: OnCodeChanged);

    public static readonly BindableProperty IsErrorProperty =
        BindableProperty.Create(nameof(IsError), typeof(bool), typeof(CodeInputControl),
            false, propertyChanged: OnIsErrorChanged);

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public bool IsError
    {
        get => (bool)GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    /// <summary>
    /// Событие возникает при заполнении всех 6 боксов кода.
    /// </summary>
    public event EventHandler? CodeCompleted;

    public CodeInputControl()
    {
        InitializeComponent();
        BuildBoxes();
    }

    /// <summary>РЎРѕР·РґР°С‘С‚ 6 Р±РѕРєСЃРѕРІ РІРІРѕРґР°.</summary>
    private void BuildBoxes()
    {
        for (int i = 0; i < CodeLength; i++)
        {
            var boxIndex = i;
            var entry = new Entry
            {
                WidthRequest = 28,
                HeightRequest = 44,
                FontSize = 16,
                FontFamily = "ClashDisplay",
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Keyboard = Keyboard.Text,
                MaxLength = 1,
                BackgroundColor = Colors.Transparent,
                TextColor = GetThemeColor("TextPrimary", Colors.Black),
                PlaceholderColor = GetThemeColor("TextFaint", Colors.Gray),
                Placeholder = "A",
                ReturnType = boxIndex < CodeLength - 1 ? ReturnType.Next : ReturnType.Done,
                ClassId = boxIndex.ToString()
            };

            var border = new Border
            {
                WidthRequest = 28,
                HeightRequest = 44,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                BackgroundColor = GetThemeColor("SurfaceCard", Colors.White),
                Stroke = GetThemeColor("InputBorderColor", Colors.LightGray),
                StrokeThickness = 1.5,
                Padding = new Thickness(0),
                Content = entry,
                ClassId = $"border_{boxIndex}"
            };

            entry.TextChanged += (s, e) => OnBoxTextChanged(boxIndex, e);
            entry.Focused += (s, e) => OnBoxFocused(boxIndex);

            _boxes[i] = entry;
            codeContainer.Children.Add(border);
        }
    }

    private void OnBoxTextChanged(int index, TextChangedEventArgs e)
    {
        if (_isUpdatingText || index < 0 || index >= _boxes.Length)
        {
            return;
        }

        var text = e.NewTextValue;

        if (string.IsNullOrEmpty(text))
        {
            ClearError();
            return;
        }

        var value = char.ToUpperInvariant(text[^1]);
        if (!InviteCodeLimits.Alphabet.Contains(value))
        {
            SetBoxText(index, string.Empty);
            return;
        }

        SetBoxText(index, value.ToString());
        UpdateCode();
        ClearError();

        if (index < CodeLength - 1)
        {
            _boxes[index + 1].Focus();
        }
        else
        {
            _boxes[index].Unfocus();
            CodeCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnBoxFocused(int index)
    {
        UpdateBorderStates();
    }

    private void UpdateCode()
    {
        Code = string.Concat(_boxes.Select(b => b.Text));
    }

    private void SetBoxText(int index, string value)
    {
        if (_boxes[index].Text == value)
        {
            return;
        }

        _isUpdatingText = true;
        _boxes[index].Text = value;
        _isUpdatingText = false;
    }

    private void UpdateBorderStates()
    {
        for (int i = 0; i < CodeLength; i++)
        {
            if (GetBorder(i) is not Border border)
            {
                continue;
            }

            bool isFocused = _boxes[i].IsFocused;
            bool hasText = !string.IsNullOrEmpty(_boxes[i].Text);

            border.Stroke = isFocused
                ? GetThemeColor("InputBorderFocused", Colors.Teal)
                : hasText
                    ? GetThemeColor("Primary", Colors.Teal)
                    : GetThemeColor("InputBorderColor", Colors.LightGray);

            border.BackgroundColor = hasText
                ? GetThemeColor("SurfacePrimarySoft", Colors.LightGray)
                : GetThemeColor("SurfaceCard", Colors.White);
        }
    }

    /// <summary>РџРѕРєР°Р·С‹РІР°РµС‚ РѕС€РёР±РєСѓ: РєСЂР°СЃРЅР°СЏ СЂР°РјРєР° + shake-Р°РЅРёРјР°С†РёСЏ.</summary>
    public async Task ShowErrorAsync()
    {
        if (_isShaking)
        {
            return;
        }

        _isShaking = true;
        IsError = true;

        foreach (var box in _boxes)
        {
            if (GetBorder(int.Parse(box.ClassId ?? "0")) is Border border)
            {
                border.Stroke = GetThemeColor("InputBorderError", Colors.Red);
            }
        }

        var originalX = codeContainer.TranslationX;
        var shakeSequence = new[] { -8, 8, -6, 6, -4, 4, 0 };

        foreach (var offset in shakeSequence)
        {
            await codeContainer.TranslateTo(offset, 0, 50, Easing.Linear);
        }

        codeContainer.TranslationX = originalX;
        _isShaking = false;
    }

    /// <summary>РЎР±СЂР°СЃС‹РІР°РµС‚ СЃРѕСЃС‚РѕСЏРЅРёРµ РѕС€РёР±РєРё.</summary>
    public void ClearError()
    {
        IsError = false;
        for (int i = 0; i < CodeLength; i++)
        {
            if (GetBorder(i) is Border border)
            {
                border.Stroke = GetThemeColor("InputBorderColor", Colors.LightGray);
            }
        }
    }

    /// <summary>РћС‡РёС‰Р°РµС‚ РІСЃРµ Р±РѕРєСЃС‹ Рё СЃР±СЂР°СЃС‹РІР°РµС‚ С„РѕРєСѓСЃ РЅР° РїРµСЂРІС‹Р№.</summary>
    public void Reset()
    {
        foreach (var box in _boxes)
        {
            box.Text = string.Empty;
        }

        ClearError();
        _boxes[0].Focus();
    }

    private Border? GetBorder(int index)
    {
        return codeContainer.Children[index] as Border;
    }

    private static void OnCodeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // РЎРІРѕР№СЃС‚РІРѕ РѕР±РЅРѕРІРёР»РѕСЃСЊ вЂ” СѓРІРµРґРѕРјР»СЏРµРј UI
    }

    private static void OnIsErrorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CodeInputControl control && (bool)newValue)
        {
            _ = control.ShowErrorAsync();
        }
    }

    private static Color GetThemeColor(string resourceKey, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true && value is Color color)
        {
            return color;
        }

        return fallback;
    }
}
