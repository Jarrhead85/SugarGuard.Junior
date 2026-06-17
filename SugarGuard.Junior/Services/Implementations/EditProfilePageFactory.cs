using Microsoft.Extensions.DependencyInjection;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Implementations;

public class EditProfilePageFactory : IEditProfilePageFactory
{
    private readonly IServiceProvider _serviceProvider;

    public EditProfilePageFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public EditProfilePage Create(string childId)
    {
        var page = _serviceProvider.GetRequiredService<EditProfilePage>();
        page.ChildId = childId;
        page.IsNewChild = false;
        page.ParentUserId = null;
        return page;
    }

    public EditProfilePage CreateNew(string childId, string parentUserId)
    {
        var page = _serviceProvider.GetRequiredService<EditProfilePage>();
        page.ChildId = childId;
        page.IsNewChild = true;
        page.ParentUserId = parentUserId;
        return page;
    }
}
