using Microsoft.Extensions.DependencyInjection;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Implementations;

public class AccessManagementPageFactory : IAccessManagementPageFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AccessManagementPageFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public AccessManagementPage Create()
    {
        return _serviceProvider.GetRequiredService<AccessManagementPage>();
    }
}
