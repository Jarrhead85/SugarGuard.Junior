using Microsoft.Maui.Controls;
using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Фабрика для создания модальной страницы рекомендации (устраняет зависимость ViewModel от View).
/// </summary>
public interface IRecommendationModalFactory
{
    /// <summary>
    /// Создаёт страницу модального окна рекомендации с переданным ViewModel.
    /// </summary>
    ContentPage Create(RecommendationModalViewModel viewModel);
}
