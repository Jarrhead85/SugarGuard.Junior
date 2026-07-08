using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Application.Services;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Tests.Application.Services;

public sealed class RecommendationServiceTests
{
    [Fact]
    public async Task SaveRecommendationAsync_WhenMeasurementIsNotSynced_SavesWithoutMeasurementLink()
    {
        var options = new DbContextOptionsBuilder<SugarGuard.API.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var factory = new TestAppDbContextFactory(options);
        var child = new Child
        {
            ChildId = Guid.NewGuid(),
            FirstName = "Тест",
            LastName = "Ребёнок",
            DateOfBirth = new DateOnly(2014, 1, 1),
            DiabetesType = "Type1"
        };

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Children.Add(child);
            await db.SaveChangesAsync();
        }

        var service = new RecommendationService(
            factory,
            Mock.Of<IGlucoseStatusService>(),
            Mock.Of<ILogger<RecommendationService>>());

        var result = await service.SaveRecommendationAsync(
            child.ChildId,
            Guid.NewGuid(),
            14.0m,
            new GigaChatResponse
            {
                RecommendationText = "Сообщи взрослому.",
                Urgency = "HIGH",
                ModelUsed = "SafetyRules",
                IsSuccess = true
            });

        Assert.Null(result.MeasurementId);

        await using var verificationDb = await factory.CreateDbContextAsync();
        var persisted = await verificationDb.AIRecommendations.FindAsync(result.RecommendationId);
        Assert.NotNull(persisted);
        Assert.Null(persisted!.MeasurementId);
        Assert.Equal("Сообщи взрослому.", persisted.RecommendationText);
    }
}
