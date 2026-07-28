namespace SugarGuard.Tests.Unit.Bot;

/// <summary>Проверяет жизненный цикл сценария ввода кода через инлайн-кнопку.</summary>
public class ConnectionCodeEntrySessionServiceTests
{
    [Fact]
    public void Begin_MarksUserAsAwaitingCode()
    {
        var service = new SugarGuard.Bot.Services.ConnectionCodeEntrySessionService();

        service.Begin(12345);

        Assert.True(service.IsAwaitingCode(12345));
    }

    [Fact]
    public void Complete_RemovesAwaitingState()
    {
        var service = new SugarGuard.Bot.Services.ConnectionCodeEntrySessionService();
        service.Begin(12345);

        service.Complete(12345);

        Assert.False(service.IsAwaitingCode(12345));
    }

    [Fact]
    public void Sessions_AreIsolatedByTelegramUser()
    {
        var service = new SugarGuard.Bot.Services.ConnectionCodeEntrySessionService();
        service.Begin(12345);

        Assert.True(service.IsAwaitingCode(12345));
        Assert.False(service.IsAwaitingCode(54321));
    }
}
