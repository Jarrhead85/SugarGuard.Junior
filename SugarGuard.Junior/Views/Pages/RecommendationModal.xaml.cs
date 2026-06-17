using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Модальное окно для отображения рекомендации.
/// </summary>
public partial class RecommendationModal : ContentPage
{
    // Храним ссылку на ViewModel
    private readonly RecommendationModalViewModel _viewModel;

    /// <summary>
    /// Создает модальное окно рекомендации и назначает BindingContext.
    /// </summary>
    public RecommendationModal(RecommendationModalViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;
    }
}
