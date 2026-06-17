using Polly;
using Polly.Extensions.Http;

namespace SugarGuard.API.Policies;

/// <summary>
/// Конфигурация политик Polly для GigaChat API
/// </summary>
public static class GigaChatPolicyConfiguration
{
    /// <summary>
    /// Получить политику повторных попыток для GigaChat
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() 
            .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)), // 1s, 2s, 4s
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    logger.LogWarning(
                        "GigaChat retry #{RetryCount} after {Delay}s. Status: {StatusCode}",
                        retryCount, 
                        timespan.TotalSeconds, 
                        outcome.Result?.StatusCode);
                });
    }
    
    /// <summary>
    /// Получить политику circuit breaker для GigaChat
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, timespan) =>
                {
                    logger.LogError(
                        "Circuit breaker OPEN for {Duration}s. GigaChat unavailable. Status: {StatusCode}",
                        timespan.TotalSeconds,
                        outcome.Result?.StatusCode);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker RESET - GigaChat service restored");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Circuit breaker HALF-OPEN - Testing GigaChat service");
                });
    }
}
