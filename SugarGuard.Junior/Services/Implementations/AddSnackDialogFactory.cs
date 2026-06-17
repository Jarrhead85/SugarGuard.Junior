using Microsoft.Extensions.DependencyInjection;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.ViewModels;
using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Реализация фабрики диалога добавления перекуса: резолвит диалог и ViewModel из DI.
/// </summary>
public class AddSnackDialogFactory : IAddSnackDialogFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AddSnackDialogFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public (AddSnackDialog Dialog, AddSnackDialogViewModel ViewModel) Create()
    {
        var dialog = _serviceProvider.GetRequiredService<AddSnackDialog>();
        var viewModel = _serviceProvider.GetRequiredService<AddSnackDialogViewModel>();
        return (dialog, viewModel);
    }
}
