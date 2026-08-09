namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>Открывает приложение, которое передаёт CGM-данные локально.</summary>
public interface ICgmBridgeLauncher
{
    Task<bool> OpenAsync(string provider);
}
