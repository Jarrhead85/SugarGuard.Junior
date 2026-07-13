using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Core.Security;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Repositories.Implementations;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Infrastructure.Handlers;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Implementations;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.ViewModels;
using SugarGuard.Junior.Views.Pages;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.Extensions;

/// <summary>
/// Extension methods for organizing DI registrations by architectural layers
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register Domain Layer services - validators and business rules
    /// </summary>
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// Register Application Layer services - business scenarios and orchestration
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Core business services
        // (Singleton для сервисов, используемых MainPageViewModel, чтобы избежать
        // захвата Scoped-зависимостей в Singleton-ViewModel)
        services.AddSingleton<IMeasurementService, MeasurementService>();
        services.AddSingleton<IBackpackService, BackpackService>();
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IScheduleService, ScheduleService>();
        // Фабрика для разрыва цикла INotificationService <-> IScheduleService (ScheduleService получает уведомления лениво)
        services.AddScoped<Func<INotificationService>>(sp => () => sp.GetRequiredService<INotificationService>());
        
        // Synchronization services
        services.AddSingleton<ISyncService, SyncService>();
        services.AddSingleton<ISyncConflictResolver, SyncConflictResolver>();
        
        // AI Recommendation services
        services.AddSingleton<IAIRecommendationService, AIRecommendationService>();
        services.AddSingleton<IFallbackRecommendationService, FallbackRecommendationService>();
        services.AddSingleton<IRecommendationOrchestrator, RecommendationOrchestrator>();
        
        // Recommendation cache (singleton for app-wide cache)
        services.AddSingleton<IRecommendationCacheService, RecommendationCacheService>();
        services.AddSingleton<IRecommendationCache, RecommendationCacheAdapter>();

        // Factories (eliminate Service Locator in ViewModels)
        services.AddSingleton<IAddSnackDialogFactory, AddSnackDialogFactory>();
        services.AddSingleton<IRecommendationModalViewModelFactory, RecommendationModalViewModelFactory>();
        services.AddSingleton<IRecommendationModalFactory, RecommendationModalFactory>();

        services.AddSingleton<ICurrentUserService, CurrentUserService>();

        // Theme and scaling
        services.AddSingleton<IThemeService, ThemeService>();

        return services;
    }

    /// <summary>
    /// Register Infrastructure Layer services - repositories, HTTP clients, external dependencies
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Database context factory
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "sugarguard.db");
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Filename={dbPath}"));
        
        // Repositories (Singleton — фабрикуют короткоживущие контексты через IDbContextFactory)
        services.AddSingleton<IMeasurementRepository, MeasurementRepository>();
        services.AddSingleton<IBackpackRepository, BackpackRepository>();
        services.AddSingleton<IChildRepository, ChildRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IDiabetesSettingsRepository, DiabetesSettingsRepository>();
        
        // Security services
        services.AddSingleton<IPlatformKeyProvider, MauiSecureStorageKeyProvider>();
        services.AddSingleton<AesGcmEncryptionService>();
        services.AddSingleton<LegacyAesCbcDecryptionService>();
        // Полное имя, чтобы избежать конфликта с SugarGuard.Junior.Services.Interfaces.IEncryptionService.
        services.AddSingleton<Core.Security.IEncryptionService, VersionedEncryptionService>();
        services.AddSingleton<ICryptoService, MauiEncryptionService>();
        services.AddSingleton<MauiReEncryptJob>();
        services.AddSingleton<ISecureStorageService, SecureStorageService>();
        services.AddSingleton<IPasswordHashingService, PasswordHashingService>();
        services.AddTransient<JwtAuthorizationHandler>();
        services.AddSingleton<IAppDiagnosticsLogService, FileAppDiagnosticsLogService>();
        services.AddSingleton<ILoggerProvider, FileDiagnosticsLoggerProvider>();

        // HTTP clients and external services
        services.AddHttpClient<IApiClient, RealApiClient>(client =>
        {
            client.BaseAddress = new Uri(AppConstants.SugarGuardApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<JwtAuthorizationHandler>();

        services.AddHttpClient<ILinkService, LinkService>(client =>
        {
            client.BaseAddress = new Uri(AppConstants.SugarGuardApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<JwtAuthorizationHandler>();

        services.AddHttpClient<IAppUpdateService, AppUpdateService>(client =>
        {
            client.BaseAddress = new Uri(AppConstants.SugarGuardApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Platform services
        services.AddSingleton<ILocationService, LocationService>();
        
        return services;
    }

    /// <summary>
    /// Register Presentation Layer services - ViewModels and Pages
    /// </summary>
    public static IServiceCollection AddPresentationServices(this IServiceCollection services)
    {
        services.AddSingleton<MainPageViewModel>();
        services.AddSingleton<ProfilePageViewModel>();
        services.AddSingleton<BackpackPageViewModel>();
        services.AddSingleton<ChartPageViewModel>();
        services.AddSingleton<SchedulePageViewModel>();
        services.AddTransient<AddSnackDialogViewModel>();
        services.AddTransient<RecommendationModalViewModel>();
        services.AddTransient<EditProfilePageViewModel>();
        services.AddTransient<DiabetesSettingsPageViewModel>();
        services.AddTransient<LoginPageViewModel>();
        services.AddTransient<RegisterPageViewModel>();
        services.AddTransient<VerifyPageViewModel>();
        services.AddTransient<OnboardingPageViewModel>();
        services.AddTransient<AccessManagementPageViewModel>();
        services.AddTransient<HelpAlertPageViewModel>();
        services.AddTransient<SupportPageViewModel>();
        services.AddSingleton<HistoryPageViewModel>();
        services.AddSingleton<NutritionTrackerPageViewModel>();

        // Pages (Singleton for main pages, Transient for dialogs and pushed pages)
        services.AddSingleton<MainPage>();
        services.AddSingleton<ProfilePage>();
        services.AddSingleton<BackpackPage>();
        services.AddSingleton<ChartPage>();
        services.AddSingleton<SchedulePage>();
        services.AddTransient<AddSnackDialog>();
        services.AddTransient<RecommendationModal>();
        services.AddTransient<EditProfilePage>();
        services.AddTransient<DiabetesSettingsPage>();
        services.AddTransient<LoginPage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<VerifyPage>();
        services.AddTransient<OnboardingPage>();
        services.AddTransient<AccessManagementPage>();
        services.AddTransient<HelpAlertPage>();
        services.AddTransient<SupportPage>();
        services.AddTransient<PrivacyPage>();
        services.AddSingleton<HistoryPage>();
        services.AddSingleton<NutritionTrackerPage>();

        // Page factories (for navigation with childId)
        services.AddSingleton<IEditProfilePageFactory, EditProfilePageFactory>();
        services.AddSingleton<IDiabetesSettingsPageFactory, DiabetesSettingsPageFactory>();
        services.AddSingleton<IAccessManagementPageFactory, AccessManagementPageFactory>();

        // Shell
        services.AddSingleton<AppShell>();
        
        return services;
    }
}
