using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Страница для настройки расписания измерений глюкозы.
/// </summary>
public partial class SchedulePage : SwipeablePage
{
    private readonly SchedulePageViewModel _viewModel;

    public SchedulePage(SchedulePageViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <summary>
    /// Вызывается при появлении страницы.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SchedulePage OnAppearing error: {ex}");
            await DisplayAlert("Ошибка", $"Не удалось загрузить расписание: {ex.Message}", "ОК");
        }
    }

    /// <summary>
    /// Обработчик изменения текста в поле времени, автоматически приводит ввод к формату ЧЧ:ММ.
    /// </summary>
    private void OnTimeEntryTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        var text = e.NewTextValue;

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Оставляем только цифры
        var digitsOnly = new string(text.Where(char.IsDigit).ToArray());

        // В формате ЧЧ:ММ нужно не больше 4 цифр.
        if (digitsOnly.Length > 4)
        {
            digitsOnly = digitsOnly[..4];
        }

        // Постепенно форматируем ввод:
        var formatted = digitsOnly.Length switch
        {
            0 => string.Empty,
            1 => digitsOnly,
            2 => digitsOnly,
            3 => $"{digitsOnly[..2]}:{digitsOnly.Substring(2, 1)}",
            4 => $"{digitsOnly[..2]}:{digitsOnly.Substring(2, 2)}",
            _ => digitsOnly
        };

        // Обновляем текст только если формат реально изменился.
        if (formatted != text)
        {
            entry.Text = formatted;
        }
    }
}
