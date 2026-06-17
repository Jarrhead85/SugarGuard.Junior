using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Фабрика страницы настроек диабета (с передачей childId перед показом).
/// </summary>
public interface IDiabetesSettingsPageFactory
{
    DiabetesSettingsPage Create(string childId);
}
