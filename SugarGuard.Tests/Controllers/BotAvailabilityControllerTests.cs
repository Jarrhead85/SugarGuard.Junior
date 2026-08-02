using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Controllers;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.Tests.Controllers;

/// <summary>
/// Проверяет безопасный статус Telegram-канала для кабинета и мобильного клиента.
/// </summary>
public sealed class BotAvailabilityControllerTests
{
    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        var authorization = typeof(BotAvailabilityController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorization);
    }

    [Fact]
    public async Task GetTelegramAvailability_WithoutHeartbeat_ReturnsGenericDegradedStatus()
    {
        await using var db = CreateDb();
        var controller = new BotAvailabilityController(db);

        var result = await controller.GetTelegramAvailability(CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<TelegramBotAvailabilityResponse>(response.Value);
        Assert.False(payload.IsAvailable);
        Assert.Contains("временно недоступен", payload.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTelegramAvailability_WithFreshSuccessfulHeartbeat_ReturnsAvailable()
    {
        await using var db = CreateDb();
        db.BotServiceHeartbeats.Add(new BotServiceHeartbeat
        {
            BotName = "telegram",
            LastHeartbeatAt = DateTime.UtcNow,
            LastExternalApiSuccessAt = DateTime.UtcNow,
            InternetAvailable = true
        });
        await db.SaveChangesAsync();

        var controller = new BotAvailabilityController(db);

        var result = await controller.GetTelegramAvailability(CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<TelegramBotAvailabilityResponse>(response.Value);
        Assert.True(payload.IsAvailable);
        Assert.Equal("Telegram-бот работает.", payload.Message);
    }

    [Fact]
    public async Task GetTelegramAvailability_WithInternalError_DoesNotExposeInfrastructureDetails()
    {
        const string internalError = "Happ proxy 127.0.0.1:10809 refused a connection";
        await using var db = CreateDb();
        db.BotServiceHeartbeats.Add(new BotServiceHeartbeat
        {
            BotName = "telegram",
            LastHeartbeatAt = DateTime.UtcNow,
            LastExternalApiSuccessAt = DateTime.UtcNow,
            InternetAvailable = true,
            LastError = internalError
        });
        await db.SaveChangesAsync();

        var controller = new BotAvailabilityController(db);

        var result = await controller.GetTelegramAvailability(CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<TelegramBotAvailabilityResponse>(response.Value);
        Assert.False(payload.IsAvailable);
        Assert.DoesNotContain(internalError, payload.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", payload.Message, StringComparison.Ordinal);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }
}
