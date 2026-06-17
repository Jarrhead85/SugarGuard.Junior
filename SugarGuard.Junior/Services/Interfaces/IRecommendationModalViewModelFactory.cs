using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Фабрика для создания ViewModel модального окна рекомендации (устраняет Service Locator в MainPageViewModel).
/// </summary>
public interface IRecommendationModalViewModelFactory
{
    /// <summary>
    /// Создаёт новый экземпляр RecommendationModalViewModel для одного открытия модалки.
    /// </summary>
    RecommendationModalViewModel Create();
}
