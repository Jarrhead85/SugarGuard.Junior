using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Фабрика страницы редактирования профиля (с передачей childId перед показом).
/// </summary>
public interface IEditProfilePageFactory
{
    EditProfilePage Create(string childId);
    EditProfilePage CreateNew(string childId, string parentUserId);
}
