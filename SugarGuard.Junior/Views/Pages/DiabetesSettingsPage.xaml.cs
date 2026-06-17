// Страница настроек диабета
using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Страница для настройки параметров диабета
/// Поля: целевой диапазон, чувствительность к инсулину, коэффициент углеводов-инсулина, инсулины
/// </summary>
public partial class DiabetesSettingsPage : ContentPage
{
    private readonly DiabetesSettingsPageViewModel _viewModel;

    /// <summary>
    /// ID ребёнка для загрузки настроек (устанавливается перед показом страницы).
    /// </summary>
    public string? ChildId { get; set; }

    public DiabetesSettingsPage(DiabetesSettingsPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <summary>
    /// Вызывается при появлении страницы. Загружает настройки диабета для редактирования.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var id = ChildId;
        if (!string.IsNullOrEmpty(id))
        {
            await _viewModel.LoadSettingsAsync(id);
        }
    }
}