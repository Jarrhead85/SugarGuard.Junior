using SugarGuard.API.Application.Interfaces;

namespace SugarGuard.API.Application.Services;

/// <summary>Регистрирует рабочий webhook MAX после запуска приложения.</summary>
public sealed class MaxWebhookRegistrationService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaxWebhookRegistrationService> _logger;

    public MaxWebhookRegistrationService(IServiceScopeFactory scopeFactory, ILogger<MaxWebhookRegistrationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMaxBotService>().RegisterWebhookAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось зарегистрировать webhook MAX.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
