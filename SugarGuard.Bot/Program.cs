using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SugarGuard.Bot.Handlers;
using SugarGuard.Bot.Services;
using SugarGuard.Bot.Keyboards;
using Telegram.Bot;

namespace SugarGuard.Bot;

/// <summary>
/// Главный класс приложения Telegram-бота SugarGuard
/// </summary>
public class Program
{
    /// <summary>
    /// Точка входа в приложение
    /// </summary>
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetService<ILogger<Program>>();
            logger?.LogCritical(ex, "Критическая ошибка при запуске бота");
            throw;
        }
    }

    /// <summary>
    /// Создаёт и настраивает хост приложения
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", 
                    optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                
                // Загружаем Telegram Bot Token из переменной окружения с fallback на appsettings.json
                var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
                    ?? configuration["BotSettings:Token"];
                
                if (string.IsNullOrWhiteSpace(botToken))
                {
                    throw new InvalidOperationException(
                        "Переменная окружения TELEGRAM_BOT_TOKEN обязательна. " +
                        "Пожалуйста, установите переменную окружения или настройте её в appsettings.json для разработки.");
                }
                
                // Регистрируем Telegram Bot Client
                services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));
                
                // Регистрируем HTTP клиент для API.
                const string ApiHttpClientName = "SugarGuardBotApi";
                services.AddHttpClient(ApiHttpClientName, client =>
                {
                    // Конкретные значения (BaseAddress, User-Agent) ApiClient проставит сам
                    // в конструкторе, чтобы не дублировать конфигурацию.
                });
                services.AddSingleton<Services.ApiClient>(sp =>
                {
                    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                    var httpClient = httpClientFactory.CreateClient(ApiHttpClientName);
                    var logger = sp.GetRequiredService<ILogger<Services.ApiClient>>();
                    var config = sp.GetRequiredService<IConfiguration>();
                    return new Services.ApiClient(httpClient, logger, config);
                });
                services.AddHttpClient<IBotUserContextService, BotUserContextService>();

                // Регистрируем сервисы
                services.AddSingleton<TelegramRateLimiter>();
                services.AddSingleton<CommandHandler>();
                services.AddSingleton<CallbackHandler>();
                services.AddSingleton<MessageHandler>();
                services.AddSingleton<BackpackBotService>();
                services.AddSingleton<StatisticsBotService>();
                
                // Регистрируем клавиатуры
                services.AddSingleton<MainMenuKeyboard>();
                services.AddSingleton<BackpackKeyboard>();
                services.AddSingleton<StatisticsKeyboard>();
                
                // Регистрируем основной сервис бота
                services.AddHostedService<BotService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });
}
