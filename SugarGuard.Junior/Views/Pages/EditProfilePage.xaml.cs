// Страница редактирования профиля ребёнка
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Страница для редактирования профиля ребёнка
/// Поля: имя, фамилия, дата рождения, вес, рост, тип диабета
/// Автоматический расчёт ИМТ при изменении веса/роста
/// </summary>
public partial class EditProfilePage : ContentPage
{
    private readonly EditProfilePageViewModel _viewModel;
    private readonly ILogger<EditProfilePage>? _logger;

    /// <summary>
    /// ID ребёнка для загрузки (устанавливается перед показом страницы, например из фабрики).
    /// </summary>
    public string? ChildId { get; set; }
    public string? ParentUserId { get; set; }
    public bool IsNewChild { get; set; }

    public EditProfilePage(EditProfilePageViewModel viewModel, ILogger<EditProfilePage>? logger = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _logger = logger;
        BindingContext = _viewModel;
    }

    /// <summary>
    /// Вызывается при появлении страницы. Загружает данные профиля для редактирования.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var id = ChildId;
        if (string.IsNullOrEmpty(id)) return;

        try
        {
            if (IsNewChild)
            {
                if (string.IsNullOrWhiteSpace(ParentUserId))
                {
                    await DisplayAlert("Ошибка", "Не удалось определить владельца профиля.", "ОК");
                    return;
                }

                _viewModel.StartNewChildDraft(id, ParentUserId);
            }
            else
            {
                await _viewModel.LoadProfileAsync(id);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "EditProfilePage: ошибка загрузки профиля {ChildId}", id);
            await DisplayAlert("Ошибка", "Не удалось загрузить профиль", "ОК");
        }
    }
}
