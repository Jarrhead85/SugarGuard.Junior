using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Фабрика страницы управления доступом, чтобы открывать её через DI.
/// </summary>
public interface IAccessManagementPageFactory
{
    AccessManagementPage Create();
}
