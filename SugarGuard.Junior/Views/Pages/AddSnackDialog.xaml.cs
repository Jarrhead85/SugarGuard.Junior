// Код-бихайнд диалога добавления перекуса
using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Диалог добавления перекуса в рюкзак
/// </summary>
public partial class AddSnackDialog : ContentPage
{
    private AddSnackDialogViewModel? _viewModel;

    public AddSnackDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Инициализация диалога с ViewModel и ID ребёнка
    /// </summary>
    public void Initialize(AddSnackDialogViewModel viewModel, string childId)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        
        // Подписываемся на событие закрытия
        _viewModel.DialogClosed += OnDialogClosed;
        
        // Инициализируем ViewModel
        _viewModel.Initialize(childId);
    }

    /// <summary>
    /// Обработчик закрытия диалога
    /// </summary>
    private async void OnDialogClosed(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.DialogClosed -= OnDialogClosed;
        }
        
        await Navigation.PopModalAsync();
    }

    /// <summary>
    /// Очистка при закрытии страницы
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        if (_viewModel != null)
        {
            _viewModel.DialogClosed -= OnDialogClosed;
        }
    }
}
