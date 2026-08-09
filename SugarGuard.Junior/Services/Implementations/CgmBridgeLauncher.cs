using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// SugarGuard не подключается к сенсору по Bluetooth и не хранит логины
/// производителя: он лишь открывает выбранный локальный bridge для настройки
/// Android-broadcast.
/// </summary>
public sealed class CgmBridgeLauncher : ICgmBridgeLauncher
{
    public Task<bool> OpenAsync(string provider)
    {
#if ANDROID
        var packageName = provider.StartsWith("Juggluco", StringComparison.OrdinalIgnoreCase)
            ? "tk.glucodata"
            : "com.eveningoutpost.dexdrip";
        var context = Android.App.Application.Context;
        var intent = context.PackageManager?.GetLaunchIntentForPackage(packageName);
        if (intent is null)
        {
            return Task.FromResult(false);
        }

        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        context.StartActivity(intent);
        return Task.FromResult(true);
#else
        return Task.FromResult(false);
#endif
    }
}
