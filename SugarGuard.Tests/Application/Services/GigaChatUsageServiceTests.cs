using Microsoft.Extensions.Configuration;
using SugarGuard.API.Application.Services;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Tests.Application.Services;

public sealed class GigaChatUsageServiceTests
{
    [Fact]
    public async Task GetAsync_UsesProviderBillableUsageWithoutSubtractingCachedTokensTwice()
    {
        var factory = new TestAppDbContextFactory($"GigaChatUsage_{Guid.NewGuid():N}");
        var child = new Child
        {
            ChildId = Guid.NewGuid(),
            FirstName = "Тест",
            LastName = "Ребёнок",
            DateOfBirth = new DateOnly(2014, 1, 1),
            DiabetesType = "Type1"
        };
        var conversation = new AiConversation
        {
            ConversationId = Guid.NewGuid(),
            ChildId = child.ChildId,
            Child = child,
            CreatedAt = DateTime.UtcNow
        };
        var message = new AiConversationMessage
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conversation.ConversationId,
            Conversation = conversation,
            Role = AiMessageRole.Assistant,
            Text = "Безопасный ответ.",
            InputTokens = 300,
            OutputTokens = 50,
            PrecachedPromptTokens = 120,
            PromptVersion = "sugarguard-clinical-v3",
            CreatedAt = DateTime.UtcNow
        };

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Children.Add(child);
            db.Set<AiConversation>().Add(conversation);
            db.Set<AiConversationMessage>().Add(message);
            await db.SaveChangesAsync();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GigaChat:MonthlyTokenBudget"] = "250"
            })
            .Build();
        var service = new GigaChatUsageService(factory, configuration);

        var usage = await service.GetAsync();

        Assert.Equal(300, usage.Month.InputTokens);
        Assert.Equal(120, usage.Month.PrecachedPromptTokens);
        Assert.Equal(350, usage.Month.TotalTokens);
        Assert.Equal(0, usage.MonthlyTokensRemaining);
        var version = Assert.Single(usage.PromptVersions);
        Assert.Equal("sugarguard-clinical-v3", version.PromptVersion);
        Assert.Equal(350, version.Month.TotalTokens);
    }
}
