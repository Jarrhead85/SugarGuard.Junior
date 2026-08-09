using Microsoft.Extensions.DependencyInjection;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Implementations;

public sealed class CgmSetupPageFactory(IServiceProvider serviceProvider) : ICgmSetupPageFactory
{
    public CgmSetupPage Create() => serviceProvider.GetRequiredService<CgmSetupPage>();
}
