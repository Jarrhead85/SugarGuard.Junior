using SugarGuard.Junior.ViewModels;
using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Фабрика для создания диалога добавления перекуса и его ViewModel (устраняет Service Locator в BackpackPageViewModel).
/// </summary>
public interface IAddSnackDialogFactory
{
    /// <summary>
    /// Создаёт новый экземпляр диалога и ViewModel для одного показа.
    /// </summary>
    (AddSnackDialog Dialog, AddSnackDialogViewModel ViewModel) Create();
}
