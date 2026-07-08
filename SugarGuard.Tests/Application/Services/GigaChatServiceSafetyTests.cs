using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Application.Services;

namespace SugarGuard.Tests.Application.Services;

public sealed class GigaChatServiceSafetyTests
{
    [Fact]
    public async Task GetRecommendationAsync_WhenGlucoseIsVeryHigh_ReturnsSafetyRecommendationWithoutHttpCall()
    {
        var handler = new ThrowingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GigaChat:ClientId"] = "test",
                ["GigaChat:ClientSecret"] = "test"
            })
            .Build();
        var service = new GigaChatService(
            httpClient,
            configuration,
            NullLogger<GigaChatService>.Instance,
            context: null!,
            new GigaChatTokenCache());

        var result = await service.GetRecommendationAsync(new GigaChatRequest
        {
            ChildId = Guid.NewGuid(),
            ChildAge = 10,
            DiabetesType = "1 типа",
            CurrentGlucose = 14.0,
            GlucoseStatus = "ВЫСОКО",
            TargetRangeMin = 4.0,
            TargetRangeMax = 10.0,
            AvailableSnacks = ["Яблоко (1.0 ХЕ)"]
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.IsLocalFallback);
        Assert.Equal("SafetyRules", result.ModelUsed);
        Assert.Equal("HIGH", result.Urgency);
        Assert.Contains("сообщи взрослому", result.RecommendationText);
        Assert.Equal(0, handler.SendCount);
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
}
