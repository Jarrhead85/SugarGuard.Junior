using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;

namespace SugarGuard.Tests.Application.Services;

public sealed class TelegramOutboxServiceTests
{
    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ClaimPendingAsync_LeasesMessageAndPreventsImmediateSecondClaim()
    {
        await using var context = CreateContext();
        var service = new TelegramOutboxService(context, NullLogger<TelegramOutboxService>.Instance);

        await service.QueueAsync(123456, "critical", "Проверьте уровень глюкозы");

        var firstClaim = await service.ClaimPendingAsync(10);
        var secondClaim = await service.ClaimPendingAsync(10);

        Assert.Single(firstClaim);
        Assert.Empty(secondClaim);
    }

    [Fact]
    public async Task CompleteAsync_AfterMaximumAttempts_MarksMessageAsFailed()
    {
        await using var context = CreateContext();
        var service = new TelegramOutboxService(context, NullLogger<TelegramOutboxService>.Instance);

        await service.QueueAsync(123456, "critical", "Проверьте уровень глюкозы");
        Guid messageId = Guid.Empty;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var claimed = await service.ClaimPendingAsync(1);
            var message = Assert.Single(claimed);
            messageId = message.MessageId;

            await service.CompleteAsync(message.MessageId, new TelegramOutboxDeliveryRequest
            {
                Delivered = false,
                Error = "Telegram недоступен"
            });

            var entity = await context.TelegramOutboxMessages.SingleAsync();
            entity.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
            entity.LockedUntil = null;
            await context.SaveChangesAsync();
        }

        var failed = await context.TelegramOutboxMessages.SingleAsync(item => item.TelegramOutboxMessageId == messageId);
        Assert.NotNull(failed.FailedAt);
        Assert.Empty(await service.ClaimPendingAsync(1));
    }

    [Fact]
    public async Task AcknowledgeAsync_AcceptsOnlyMessageRecipientAndPersistsDeliveryParts()
    {
        await using var context = CreateContext();
        var service = new TelegramOutboxService(context, NullLogger<TelegramOutboxService>.Instance);

        await service.QueueAsync(
            telegramUserId: 123456,
            messageType: "critical",
            text: "Критическое значение",
            latitude: 55.75,
            longitude: 37.62,
            requiresAcknowledgement: true);

        var message = Assert.Single(await service.ClaimPendingAsync(1));

        await service.MarkPartDeliveredAsync(message.MessageId, TelegramOutboxDeliveryPart.Text);
        await service.MarkPartDeliveredAsync(message.MessageId, TelegramOutboxDeliveryPart.Location);

        Assert.False(await service.AcknowledgeAsync(message.MessageId, 999999));
        Assert.True(await service.AcknowledgeAsync(message.MessageId, 123456));

        var stored = await context.TelegramOutboxMessages.SingleAsync();
        Assert.NotNull(stored.TextDeliveredAt);
        Assert.NotNull(stored.LocationDeliveredAt);
        Assert.NotNull(stored.AcknowledgedAt);
        Assert.Equal(123456, stored.AcknowledgedByTelegramUserId);
    }
}
