using Microsoft.Maui.Controls;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.ViewModels;
using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Создаёт модальную страницу рекомендации; конкретный тип View остаётся в слое представления.
/// </summary>
public class RecommendationModalFactory : IRecommendationModalFactory
{
    public ContentPage Create(RecommendationModalViewModel viewModel)
    {
        return new RecommendationModal(viewModel);
    }
}
