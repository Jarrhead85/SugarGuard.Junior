using Microcharts.Maui;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using SugarGuard.Junior.Extensions;
using SugarGuard.Junior.Services.Interfaces;
#if ANDROID
using SugarGuard.Junior.Platforms.Android.Glucose;
#endif

namespace SugarGuard.Junior;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            // Разворачиваем до самого глубокого InnerException
            var inner = ex;
            while (inner?.InnerException != null)
                inner = inner.InnerException;

            System.Diagnostics.Debug.WriteLine("=== CRASH ROOT CAUSE ===");
            System.Diagnostics.Debug.WriteLine(inner?.GetType().FullName);
            System.Diagnostics.Debug.WriteLine(inner?.Message);
            System.Diagnostics.Debug.WriteLine(inner?.StackTrace);
            System.Diagnostics.Debug.WriteLine("=== FULL EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine(ex?.ToString());
        };

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMicrocharts()
            .UseLocalNotification()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("SugarGuardBody.ttf", "SugarGuardBody");
                fonts.AddFont("SugarGuardDisplay.ttf", "SugarGuardDisplay");
                fonts.AddFont("SugarGuardBody.ttf", "Satoshi");
                fonts.AddFont("SugarGuardDisplay.ttf", "ClashDisplay");
            });

        // ЛОГИРОВАНИЕ
        builder.Services.AddLogging(logging =>
        {
#if DEBUG
            logging.AddDebug();
#endif
        });

        // DOMAIN LAYER 
        builder.Services.AddDomainServices();

        // APPLICATION LAYER 
        builder.Services.AddApplicationServices();

        // INFRASTRUCTURE LAYER 
        builder.Services.AddInfrastructureServices();

        // PRESENTATION LAYER 
        builder.Services.AddPresentationServices();

        builder.Services.AddTransient(sp =>
            new Lazy<IAuthenticationService>(sp.GetRequiredService<IAuthenticationService>));

        var app = builder.Build();

#if ANDROID
        JugglucoBroadcastRuntime.Configure(app.Services);
#endif

        return app;
    }
}
