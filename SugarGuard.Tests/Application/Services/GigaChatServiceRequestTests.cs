using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SugarGuard.API.Application.Ai;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Application.Services;

namespace SugarGuard.Tests.Application.Services;

public sealed class GigaChatServiceRequestTests
{
    [Fact]
    public async Task GetRecommendationAsync_SendsConfiguredSystemPromptAndStableProviderSessionId()
    {
        var handler = new RecordingGigaChatHandler();
        using var httpClient = new HttpClient(handler);
        var options = new GigaChatOptions
        {
            Model = "GigaChat-Pro",
            SystemPrompt = "Тестовая системная инструкция.",
            SystemPromptVersion = "test-v3",
            Temperature = 0.15,
            MaxTokens = 300,
            EnableSessionContextCache = true
        };
        var service = CreateService(httpClient, options);
        var conversationId = Guid.NewGuid();
        var request = CreateRequest(conversationId);

        var firstResponse = await service.GetRecommendationAsync(request);
        var secondResponse = await service.GetRecommendationAsync(request);

        Assert.True(firstResponse.IsSuccess);
        Assert.True(secondResponse.IsSuccess);
        Assert.Equal("GigaChat-2", firstResponse.ModelUsed);
        Assert.Equal(321, firstResponse.InputTokens);
        Assert.Equal(42, firstResponse.OutputTokens);
        Assert.Equal(210, firstResponse.PrecachedPromptTokens);
        Assert.Equal("test-v3", firstResponse.PromptVersion);
        Assert.Equal(3, handler.Requests.Count);

        var firstCompletionRequest = handler.Requests[1];
        var secondCompletionRequest = handler.Requests[2];
        var firstSessionId = firstCompletionRequest.Headers["X-Session-ID"];
        Assert.Matches($"^sg-test-v3-[0-9a-f]{{12}}-{conversationId:N}$", firstSessionId);
        Assert.Equal(firstSessionId, secondCompletionRequest.Headers["X-Session-ID"]);

        using var document = JsonDocument.Parse(firstCompletionRequest.Body);
        var messages = document.RootElement.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("Тестовая системная инструкция.", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());

        var userPrompt = messages[1].GetProperty("content").GetString();
        Assert.NotNull(userPrompt);
        Assert.Contains("<question>", userPrompt);
        Assert.Contains("Что мне сейчас делать?", userPrompt);
        Assert.Contains("<clinical_context>", userPrompt);
        Assert.Contains("Рюкзак сейчас: сок", userPrompt);
        Assert.DoesNotContain("\"availableBackpack\"", userPrompt);
        Assert.DoesNotContain("Правила ответа:", userPrompt);

        Assert.Equal("GigaChat-Pro", document.RootElement.GetProperty("model").GetString());
        Assert.Equal(0.15, document.RootElement.GetProperty("temperature").GetDouble(), 3);
        Assert.Equal(300, document.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task GetRecommendationAsync_DoesNotSendProviderSessionIdWhenCacheDisabled()
    {
        var handler = new RecordingGigaChatHandler();
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, new GigaChatOptions
        {
            SystemPromptVersion = "test-v3",
            EnableSessionContextCache = false
        });

        var response = await service.GetRecommendationAsync(CreateRequest(Guid.NewGuid()));

        Assert.True(response.IsSuccess);
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain("X-Session-ID", handler.Requests[1].Headers.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRecommendationAsync_ChangesProviderSessionIdWhenSystemInstructionChanges()
    {
        var conversationId = Guid.NewGuid();
        var firstSessionId = await GetProviderSessionIdAsync(
            new GigaChatOptions
            {
                SystemPromptVersion = "test-v3",
                SystemPrompt = "Первая инструкция."
            },
            conversationId);
        var secondSessionId = await GetProviderSessionIdAsync(
            new GigaChatOptions
            {
                SystemPromptVersion = "test-v3",
                SystemPrompt = "Вторая инструкция."
            },
            conversationId);

        Assert.NotEqual(firstSessionId, secondSessionId);
    }

    [Fact]
    public async Task GetRecommendationAsync_WhenProviderResponseIsTruncated_UsesSafeFallbackAndKeepsUsage()
    {
        var handler = new RecordingGigaChatHandler(
            finishReason: "length",
            completionText: "Незавершённый ответ от модели");
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, new GigaChatOptions
        {
            SystemPromptVersion = "test-v3"
        });

        var response = await service.GetRecommendationAsync(CreateRequest(Guid.NewGuid()));

        Assert.True(response.IsSuccess);
        Assert.True(response.IsLocalFallback);
        Assert.Equal("Local", response.ModelUsed);
        Assert.DoesNotContain("Незавершённый", response.RecommendationText);
        Assert.Equal(321, response.InputTokens);
        Assert.Equal(42, response.OutputTokens);
        Assert.Equal(210, response.PrecachedPromptTokens);
        Assert.Equal("test-v3", response.PromptVersion);
    }

    private static async Task<string> GetProviderSessionIdAsync(GigaChatOptions options, Guid conversationId)
    {
        var handler = new RecordingGigaChatHandler();
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, options);

        var response = await service.GetRecommendationAsync(CreateRequest(conversationId));

        Assert.True(response.IsSuccess);
        return handler.Requests[1].Headers["X-Session-ID"];
    }

    private static GigaChatService CreateService(HttpClient httpClient, GigaChatOptions options) => new(
        httpClient,
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GigaChat:ClientId"] = "test-client",
                ["GigaChat:ClientSecret"] = "test-secret",
                ["GigaChat:AuthUrl"] = "https://gigachat.test/oauth",
                ["GigaChat:ApiUrl"] = "https://gigachat.test/chat/completions"
            })
            .Build(),
        NullLogger<GigaChatService>.Instance,
        new GigaChatTokenCache(),
        Options.Create(options),
        Options.Create(new AiClinicalContextOptions()));

    private static GigaChatRequest CreateRequest(Guid conversationId)
    {
        var context = new ClinicalContext
        {
            Profile = new ClinicalProfileContext
            {
                AgeGroup = "10 лет",
                DiabetesType = "Type1",
                TargetRangeMin = 4m,
                TargetRangeMax = 10m
            },
            Current = new CurrentSituationContext
            {
                Measurement = new GlucoseContext
                {
                    MeasuredAt = DateTime.UtcNow,
                    Value = 6.7m
                }
            },
            AvailableBackpack =
            [
                new BackpackSnackContext
                {
                    SnackName = "сок",
                    BreadUnits = 1m
                }
            ]
        };

        return new GigaChatRequest
        {
            ChildId = Guid.NewGuid(),
            ConversationId = conversationId,
            ChildAge = 10,
            DiabetesType = "1 типа",
            CurrentGlucose = 6.7,
            GlucoseStatus = "НОРМА",
            Trend = "стабильно",
            TargetRangeMin = 4.0,
            TargetRangeMax = 10.0,
            AvailableSnacks = ["сок (1 ХЕ)"],
            Question = "Что мне сейчас делать?",
            StructuredContextJson = JsonSerializer.Serialize(context, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }

    private sealed class RecordingGigaChatHandler : HttpMessageHandler
    {
        private readonly string _completionJson;

        public RecordingGigaChatHandler(string finishReason = "stop", string completionText = "Спокойно продолжай наблюдать за самочувствием.")
        {
            _completionJson = JsonSerializer.Serialize(new
            {
                model = "GigaChat-2",
                choices = new[]
                {
                    new
                    {
                        message = new { content = completionText },
                        finish_reason = finishReason
                    }
                },
                usage = new
                {
                    prompt_tokens = 321,
                    completion_tokens = 42,
                    total_tokens = 363,
                    precached_prompt_tokens = 210
                }
            });
        }

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var headers = request.Headers.ToDictionary(
                pair => pair.Key,
                pair => string.Join(",", pair.Value),
                StringComparer.OrdinalIgnoreCase);
            Requests.Add(new RecordedRequest(request.RequestUri!, headers, body));

            return request.RequestUri!.AbsolutePath.Contains("oauth", StringComparison.OrdinalIgnoreCase)
                ? JsonResponse("""{"access_token":"token","expires_in":1800}""")
                : JsonResponse(_completionJson);
        }

        private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed record RecordedRequest(
        Uri Uri,
        IReadOnlyDictionary<string, string> Headers,
        string Body);
}
