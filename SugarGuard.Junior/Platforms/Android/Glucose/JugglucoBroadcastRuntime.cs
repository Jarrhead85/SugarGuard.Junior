#if ANDROID
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Core.Sensors;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Platforms.Android.Glucose;

/// <summary>
/// Передаёт входящий Android broadcast в DI-контейнер MAUI.
/// Получатель broadcast не хранит Activity или Context, поэтому может безопасно запускаться в фоне.
/// </summary>
public static class JugglucoBroadcastRuntime
{
    private static IServiceProvider? _services;

    /// <summary>Фиксирует контейнер после создания MAUI-приложения.</summary>
    public static void Configure(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>Асинхронно обрабатывает уже проверенное показание Juggluco.</summary>
    public static async Task ConsumeAsync(SensorGlucoseReading reading)
    {
        var services = _services;
        if (services is null)
        {
            System.Diagnostics.Debug.WriteLine("Juggluco broadcast получен до инициализации SugarGuard.");
            return;
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("JugglucoBroadcast");
        var ingestionService = services.GetRequiredService<ISensorGlucoseIngestionService>();
        var result = await ingestionService.IngestAsync(reading);

        if (!result.IsSaved && !result.IsDuplicate)
        {
            logger.LogWarning("Показание Juggluco не сохранено: {Error}", result.ErrorMessage);
        }
    }
}
#endif
