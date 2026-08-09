using Microsoft.Extensions.DependencyInjection;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Implementations;

public sealed class HelpAlertPageFactory(IServiceProvider serviceProvider) : IHelpAlertPageFactory
{
    public HelpAlertPage Create() => serviceProvider.GetRequiredService<HelpAlertPage>();
}
