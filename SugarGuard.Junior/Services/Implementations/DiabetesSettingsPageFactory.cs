using Microsoft.Extensions.DependencyInjection;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Implementations;

public class DiabetesSettingsPageFactory : IDiabetesSettingsPageFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DiabetesSettingsPageFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public DiabetesSettingsPage Create(string childId)
    {
        var page = _serviceProvider.GetRequiredService<DiabetesSettingsPage>();
        page.ChildId = childId;
        return page;
    }
}
