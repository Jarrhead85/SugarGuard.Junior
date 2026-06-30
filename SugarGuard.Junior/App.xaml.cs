using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.ViewModels;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior;

/// <summary>
/// Корневой класс приложения.
/// </summary>
public partial class App : Application
{
    // Ключ настройки темы — совпадает с ProfilePageViewModel
    private const string DarkThemePreferenceKey = "dark_theme_enabled";

    // Источники словарей тем — совпадают с именами файлов в App.xaml
    private const string LightThemeSource = "Resources/Styles/Theme.Light.xaml";
    private const string DarkThemeSource = "Resources/Styles/Theme.Dark.xaml";

    /// <summary>Логгер уровня приложения.</summary>
    private readonly ILogger<App> _logger;

    /// <summary>Фабрика контекстов БД.</summary>
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    /// <summary>
    /// Главная ViewModel.
    /// Инициализируется на старте, если пользователь уже авторизован.
    /// </summary>
    private readonly MainPageViewModel _mainPageViewModel;

    /// <summary>Сервис синхронизации локальных данных с сервером.</summary>
    private readonly ISyncService _syncService;

    /// <summary>Сервис криптографии для шифрования локально хранимых чувствительных данных.</summary>
    private readonly ICryptoService _cryptoService;

    /// <summary>Сервис локального хранения простых настроек и служебных ключей.</summary>
    private readonly IStorageService _storageService;

    /// <summary>Сервис проверки текущей сессии и состояния авторизации.</summary>
    private readonly IAuthenticationService _authenticationService;

    private readonly IThemeService _themeService;

    /// <summary>DI-контейнер для получения зарегистрированных сервисов (включая AppShell).</summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Фоновая задача миграции ciphertext из legacy AES-CBC в AES-GCM.
    /// Запускается при старте приложения.
    /// </summary>
    private readonly MauiReEncryptJob _reEncryptJob;

    /// <summary>
    /// Примитив синхронизации для защиты от повторного одновременного запуска
    /// стартовой инициализации приложения.
    /// </summary>
    private readonly SemaphoreSlim _startupSemaphore = new(1, 1);

    /// <summary>Флаг, показывающий, что первичная инициализация уже успешно выполнена.</summary>
    private bool _startupCompleted;

    public App(
        ILogger<App> logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        MainPageViewModel mainPageViewModel,
        ISyncService syncService,
        ICryptoService cryptoService,
        IStorageService storageService,
        IAuthenticationService authenticationService,
        IThemeService themeService,
        MauiReEncryptJob reEncryptJob,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _mainPageViewModel = mainPageViewModel;
        _syncService = syncService;
        _cryptoService = cryptoService;
        _storageService = storageService;
        _authenticationService = authenticationService;
        _themeService = themeService;
        _reEncryptJob = reEncryptJob;
        _serviceProvider = serviceProvider;
    }

    /// <summary>Создает главное окно приложения.</summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var appShell = _serviceProvider.GetRequiredService<AppShell>();
        return new Window(appShell);
    }

    /// <summary>Вызывается платформой при старте приложения.</summary>
    protected override async void OnStart()
    {
        try
        {
            await InitializeStartupAsync();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unhandled startup error in App.OnStart.");

            try
            {
                await NavigateToLoginAsync();
            }
            catch (Exception navigationEx)
            {
                _logger.LogCritical(navigationEx, "Fallback navigation after startup crash failed.");
            }
        }
    }

    /// <summary>Единая точка входа для стартовой инициализации приложения.</summary>
    private async Task InitializeStartupAsync()
    {
        await _startupSemaphore.WaitAsync();

        try
        {
            if (_startupCompleted)
            {
                _logger.LogInformation("Стартовая инициализация уже была выполнена ранее. Повторный запуск пропущен.");
                return;
            }

            _logger.LogInformation("Запуск приложения SugarGuard.Junior.");

            // 1. Применяем сохранённую тему интерфейса (светлая/тёмная).
            ApplySavedTheme();
            ApplySavedInterfaceSkin();

            // 2. Инициализируем криптографию до любых операций с локальными PHI-данными.
            await InitializeCryptoAsync();

            // 3. Готовим локальную базу данных.
            await InitializeDatabaseAsync();

            // Не блокирует UI. Идемпотентно.
            StartReEncryptionInBackground();

            // 4. Проверяем, есть ли действующая сессия пользователя.
            var isAuthenticated = await _authenticationService.IsAuthenticatedAsync();

            if (isAuthenticated)
                await InitializeAuthenticatedSessionFlowAsync();
            else
                await NavigateToLoginAsync();

            _startupCompleted = true;
            _logger.LogInformation("Приложение успешно инициализировано.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка во время стартовой инициализации приложения.");

            try
            {
                await NavigateToLoginAsync();
            }
            catch (Exception navigationEx)
            {
                _logger.LogError(navigationEx, "Не удалось выполнить fallback-навигацию на страницу входа.");
            }
        }
        finally
        {
            _startupSemaphore.Release();
        }
    }

    /// <summary>
    /// Применяет сохранённую пользователем тему.
    ///
    /// Делает две вещи одновременно:
    ///   1. Заменяет словарь темы в MergedDictionaries — обновляет все DynamicResource-токены
    ///      (BackgroundPage, Surface, TextPrimary, GlucoseNormalColor и т.д.)
    ///   2. Ставит UserAppTheme — обновляет AppThemeBinding в Buttons.xaml, Cards.xaml и др.
    ///
    /// Оба шага обязательны: один без другого даёт неполное переключение.
    /// </summary>
    private void ApplySavedTheme()
    {
        try
        {
            var isDarkThemeEnabled = Preferences.Get(DarkThemePreferenceKey, false);
            var targetSource = isDarkThemeEnabled ? DarkThemeSource : LightThemeSource;

            // Шаг 1: заменяем словарь темы в MergedDictionaries.
            // Ищем текущий словарь темы по имени файла и удаляем его,
            // затем добавляем нужный. DynamicResource-биндинги обновятся автоматически.
            var mergedDicts = Resources.MergedDictionaries;

            var currentThemeDict = mergedDicts
                .FirstOrDefault(d => d.Source is not null &&
                                     (d.Source.OriginalString.Contains("Theme.Light") ||
                                      d.Source.OriginalString.Contains("Theme.Dark")));

            if (currentThemeDict is not null)
                mergedDicts.Remove(currentThemeDict);

            mergedDicts.Add(new ResourceDictionary
            {
                Source = new Uri(targetSource, UriKind.Relative)
            });

            // Шаг 2: устанавливаем системную тему MAUI для AppThemeBinding.
            UserAppTheme = isDarkThemeEnabled ? AppTheme.Dark : AppTheme.Light;

            _logger.LogInformation(
                "Применена сохранённая тема: {Theme} (ключ {Key} = {Value}).",
                isDarkThemeEnabled ? "Dark" : "Light",
                DarkThemePreferenceKey,
                isDarkThemeEnabled);
        }
        catch (Exception ex)
        {
            // Fallback — светлая тема безопаснее, т.к. Theme.Light.xaml
            // всегда загружается статически в App.xaml
            UserAppTheme = AppTheme.Light;
            _logger.LogWarning(ex, "Не удалось применить сохранённую тему. Используется светлая тема по умолчанию.");
        }
    }

    /// <summary>
    /// Переключает тему во время работы приложения.
    /// Вызывается из ProfilePageViewModel при изменении настройки.
    /// </summary>
    public void SwitchTheme(bool isDark)
    {
        try
        {
            Preferences.Set(DarkThemePreferenceKey, isDark);
            ApplySavedTheme();
            ApplySavedInterfaceSkin();

            _logger.LogInformation("Тема переключена пользователем на: {Theme}.", isDark ? "Dark" : "Light");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при переключении темы.");
        }
    }

    /// <summary>Инициализирует криптографический сервис.</summary>
    private async Task InitializeCryptoAsync()
    {
        _logger.LogInformation("Инициализация криптографического сервиса...");
        await _cryptoService.InitializeAsync();
        _logger.LogInformation("Криптографический сервис успешно инициализирован.");
    }

    /// <summary>
    /// Запускает фоновую миграцию CBC → GCM (fire-and-forget).
    /// Не блокирует UI. После первой миграции идемпотентно завершается.
    /// </summary>
    private void StartReEncryptionInBackground()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _reEncryptJob.RunAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при фоновой миграции CBC → GCM.");
            }
        });
    }

    /// <summary>Инициализирует локальную базу данных.</summary>
    private async Task InitializeDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("Инициализация локальной базы данных...");
            await using var ctx = await _dbContextFactory.CreateDbContextAsync();

            if (!await HasRequiredLocalTablesAsync(ctx))
            {
                _logger.LogWarning("Локальная база данных имеет неполную схему. Выполняется пересоздание локальных таблиц.");
                await RecreateLocalDatabaseAsync(ctx);
            }
            else if (await HasAppliedMigrationsAsync(ctx))
            {
                await ctx.Database.MigrateAsync();
            }
            else
            {
                _logger.LogInformation("Локальная база создана через EnsureCreated; EF migrations пропущены.");
            }

            _logger.LogInformation("Локальная база данных готова к работе.");

#if DEBUG
            var seedEnabled = Preferences.Get("SeedEnabled", false);
            if (seedEnabled)
            {
                await SeedTestDataAsync();
            }
            else
            {
                await RemoveDebugSeedDataAsync();
            }
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации локальной базы данных.");
            throw;
        }
    }

    private void ApplySavedInterfaceSkin()
    {
        try
        {
            var skin = (InterfaceSkin)Preferences.Get("interface_skin", (int)InterfaceSkin.Neutral);
            _themeService.ApplySkin(skin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось применить сохранённый стиль интерфейса.");
        }
    }

    private async Task RecreateLocalDatabaseAsync(AppDbContext currentContext)
    {
        currentContext.Database.GetDbConnection().Close();
        await currentContext.Database.EnsureDeletedAsync();

        await using var freshContext = await _dbContextFactory.CreateDbContextAsync();
        await freshContext.Database.EnsureCreatedAsync();

        if (await HasRequiredLocalTablesAsync(freshContext))
            return;

        _logger.LogWarning("EnsureCreated не создал ожидаемую схему. Выполняется прямой schema bootstrap.");
        var createScript = freshContext.Database.GenerateCreateScript();
        var connection = freshContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = createScript;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> HasRequiredLocalTablesAsync(AppDbContext ctx)
    {
        var requiredTables = new[]
        {
            "Children",
            "Measurements",
            "BackpackItems",
            "BackpackHistory",
            "SyncQueue",
            "SnackConsumptionLogs"
        };

        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        foreach (var table in requiredTables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "$tableName";
            parameter.Value = table;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync();
            if (Convert.ToInt32(result) == 0)
                return false;
        }

        return true;
    }

    private static async Task<bool> HasAppliedMigrationsAsync(AppDbContext ctx)
    {
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';";
        if (Convert.ToInt32(await tableCommand.ExecuteScalarAsync()) == 0)
            return false;

        await using var rowsCommand = connection.CreateCommand();
        rowsCommand.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory;";
        return Convert.ToInt32(await rowsCommand.ExecuteScalarAsync()) > 0;
    }

    /// <summary>Выполняет инициализацию сессии авторизованного пользователя.</summary>
    private async Task InitializeAuthenticatedSessionFlowAsync()
    {
        _logger.LogInformation("Authenticated session found. Checking verification and onboarding flow.");

        var shell = _serviceProvider.GetRequiredService<AppShell>();

        var isEmailVerified = await _authenticationService.IsEmailVerifiedAsync();
        if (!isEmailVerified)
        {
            _logger.LogInformation("Email is not verified. Navigating to verification page.");
            await NavigateOnMainThreadAsync(shell, "//verifypage");
            return;
        }

        var onboardingCompleted = await _storageService.GetAsync("onboarding_completed");
        if (!string.Equals(onboardingCompleted, "true", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Onboarding is not completed. Navigating to onboarding page.");
            await NavigateOnMainThreadAsync(shell, "//onboardingpage");
            return;
        }

        await _mainPageViewModel.InitializeAsync();
        await _syncService.InitializeAsync();

        _logger.LogInformation("Main page and sync services initialized after startup flow checks.");
        await NavigateOnMainThreadAsync(shell, "//mainpage");
    }

    private async Task InitializeAuthenticatedSessionAsync()
    {
        _logger.LogInformation("Обнаружена активная сессия. Запускается инициализация пользовательского контекста.");

        await _mainPageViewModel.InitializeAsync();
        await _syncService.InitializeAsync();

        _logger.LogInformation("Главная ViewModel и сервис синхронизации успешно инициализированы.");

        var shell = _serviceProvider.GetRequiredService<AppShell>();

        // Проверяем, верифицирован ли email
        var isEmailVerified = await _authenticationService.IsEmailVerifiedAsync();

        if (!isEmailVerified)
        {
            _logger.LogInformation("Email не верифицирован. Перенаправляем на экран верификации.");
            await NavigateOnMainThreadAsync(shell, "//verifypage");
            return;
        }

        // Проверяем, завершён ли онбординг
        var onboardingCompleted = await _storageService.GetAsync("onboarding_completed");

        if (!string.Equals(onboardingCompleted, "true", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Онбординг не завершён. Перенаправляем на экран онбординга.");
            await NavigateOnMainThreadAsync(shell, "//onboardingpage");
            return;
        }

        await NavigateOnMainThreadAsync(shell, "//mainpage");

        _logger.LogInformation("Выполнен переход на главный экран.");
    }

    /// <summary>Переводит пользователя на страницу входа.</summary>
    private async Task NavigateToLoginAsync()
    {
        _logger.LogInformation("Активная сессия не найдена. Выполняется переход на экран входа.");
        var shell = _serviceProvider.GetRequiredService<AppShell>();
        await NavigateOnMainThreadAsync(shell, "//loginpage");
    }

    /// <summary>Добавляет тестовые данные в локальную БД для разработки.</summary>
    private static Task NavigateOnMainThreadAsync(Shell shell, string route)
    {
        return MainThread.InvokeOnMainThreadAsync(() => shell.GoToAsync(route));
    }

    private async Task SeedTestDataAsync()
    {
        try
        {
            await using var ctx = await _dbContextFactory.CreateDbContextAsync();

            var hasAnyMeasurements = await ctx.Measurements.AnyAsync();

            if (hasAnyMeasurements)
            {
                _logger.LogInformation("Тестовые данные уже существуют. Повторное заполнение пропущено.");
                return;
            }

            _logger.LogInformation("Начато добавление тестовых данных для DEBUG-сборки.");

            const string testChildId = "child-001";

            var existingChild = await ctx.Children.FindAsync(testChildId);

            if (existingChild is null)
            {
                var testChild = new Models.Core.Child
                {
                    ChildId = testChildId,
                    ParentUserId = "test-parent",
                    EncryptedFirstName = await _cryptoService.EncryptAsync("Тестовый"),
                    EncryptedLastName = await _cryptoService.EncryptAsync("Ребёнок"),
                    DateOfBirth = DateTime.Today.AddYears(-10),
                    Weight = 35,
                    Height = 140,
                    DiabetesType = Models.Enums.DiabetesType.Type1,
                    DiagnosisDate = DateTime.Today.AddYears(-2),
                    InsulinScheme = "Быстрый + длительный",
                    CurrentInsulins = "[]",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                ctx.Children.Add(testChild);
                await ctx.SaveChangesAsync();

                _logger.LogInformation("Тестовый ребёнок успешно создан.");
            }
            else
            {
                _logger.LogInformation("Тестовый ребёнок уже существует. Повторное создание не требуется.");
            }

            await _storageService.SaveAsync(AppConstants.StorageKeyCurrentChildId, testChildId);
            _logger.LogInformation("Тестовый ребёнок установлен как текущий в локальном хранилище.");

            var now = DateTime.UtcNow;

            var measurements = new[]
            {
                new MeasurementEntity
                {
                    MeasurementId        = Guid.NewGuid().ToString(),
                    ChildId              = testChildId,
                    EncryptedGlucoseValue = await _cryptoService.EncryptAsync("5.5"),
                    MeasurementTime      = now.AddHours(-2),
                    EncryptedChildState  = await _cryptoService.EncryptAsync(Models.Enums.ChildState.Normal.ToString()),
                    DataSource           = Models.Enums.DataSource.ManualInput,
                    IsSynced             = false
                },
                new MeasurementEntity
                {
                    MeasurementId        = Guid.NewGuid().ToString(),
                    ChildId              = testChildId,
                    EncryptedGlucoseValue = await _cryptoService.EncryptAsync("6.2"),
                    MeasurementTime      = now.AddHours(-1),
                    EncryptedChildState  = await _cryptoService.EncryptAsync(Models.Enums.ChildState.Normal.ToString()),
                    DataSource           = Models.Enums.DataSource.ManualInput,
                    IsSynced             = false
                },
                new MeasurementEntity
                {
                    MeasurementId        = Guid.NewGuid().ToString(),
                    ChildId              = testChildId,
                    EncryptedGlucoseValue = await _cryptoService.EncryptAsync("7.1"),
                    MeasurementTime      = now,
                    EncryptedChildState  = await _cryptoService.EncryptAsync(Models.Enums.ChildState.Normal.ToString()),
                    DataSource           = Models.Enums.DataSource.ManualInput,
                    IsSynced             = false
                }
            };

            await ctx.Measurements.AddRangeAsync(measurements);
            await ctx.SaveChangesAsync();
            _logger.LogInformation("Добавлено тестовых измерений: {Count}.", measurements.Length);

            var snacks = new[]
            {
                new BackpackItem
                {
                    ChildId               = testChildId,
                    EncryptedSnackName    = await _cryptoService.EncryptAsync("Сок"),
                    EncryptedBreadUnits   = await _cryptoService.EncryptAsync("1.5"),
                    CreatedAt             = now
                },
                new BackpackItem
                {
                    ChildId               = testChildId,
                    EncryptedSnackName    = await _cryptoService.EncryptAsync("Яблоко"),
                    EncryptedBreadUnits   = await _cryptoService.EncryptAsync("1.0"),
                    CreatedAt             = now
                },
                new BackpackItem
                {
                    ChildId               = testChildId,
                    EncryptedSnackName    = await _cryptoService.EncryptAsync("Банан"),
                    EncryptedBreadUnits   = await _cryptoService.EncryptAsync("2.0"),
                    CreatedAt             = now
                }
            };

            await ctx.BackpackItems.AddRangeAsync(snacks);
            await ctx.SaveChangesAsync();

            _logger.LogInformation("Добавлено тестовых перекусов: {Count}.", snacks.Length);
            _logger.LogInformation("Добавление тестовых данных завершено успешно.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при добавлении тестовых данных.");
        }
    }

    private async Task RemoveDebugSeedDataAsync()
    {
        const string testChildId = "child-001";

        try
        {
            await using var ctx = await _dbContextFactory.CreateDbContextAsync();

            var hasSeedChild = await ctx.Children.AnyAsync(c => c.ChildId == testChildId);
            if (!hasSeedChild)
                return;

            ctx.Measurements.RemoveRange(ctx.Measurements.Where(m => m.ChildId == testChildId));
            ctx.BackpackItems.RemoveRange(ctx.BackpackItems.Where(b => b.ChildId == testChildId));
            ctx.BackpackHistory.RemoveRange(ctx.BackpackHistory.Where(b => b.ChildId == testChildId));
            ctx.SnackConsumptionLogs.RemoveRange(ctx.SnackConsumptionLogs.Where(s => s.ChildId == testChildId));
            ctx.AIRecommendations.RemoveRange(ctx.AIRecommendations.Where(r => r.ChildId == testChildId));
            ctx.Children.RemoveRange(ctx.Children.Where(c => c.ChildId == testChildId));
            await ctx.SaveChangesAsync();

            var currentChildId = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);
            if (string.Equals(currentChildId, testChildId, StringComparison.OrdinalIgnoreCase))
            {
                await _storageService.DeleteAsync(AppConstants.StorageKeyCurrentChildId);
                await _storageService.DeleteAsync("onboarding_completed");
            }

            _logger.LogInformation("DEBUG seed data removed.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось удалить DEBUG seed data.");
        }
    }
}
