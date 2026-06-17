using Microsoft.Extensions.DependencyInjection;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Фабрика ViewModel модального окна рекомендации: создаёт новый экземпляр из DI при каждом открытии.
/// </summary>
public class RecommendationModalViewModelFactory : IRecommendationModalViewModelFactory
{
    private readonly IServiceProvider _serviceProvider;

    public RecommendationModalViewModelFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public RecommendationModalViewModel Create()
    {
        return _serviceProvider.GetRequiredService<RecommendationModalViewModel>();
    }
}
