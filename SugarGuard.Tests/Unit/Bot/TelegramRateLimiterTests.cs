using Microsoft.Extensions.Logging.Abstractions;

namespace SugarGuard.Tests.Unit.Bot;

/// <summary>
/// Unit-тесты для <see cref="SugarGuard.Bot.Services.TelegramRateLimiter"/>.
/// <para>
/// Тестируем чистый IMemoryCache-counter, без Telegram API.
/// </para>
/// </summary>
public class TelegramRateLimiterTests
{
    private static SugarGuard.Bot.Services.TelegramRateLimiter CreateLimiter(int limit = 5)
    {
        return new SugarGuard.Bot.Services.TelegramRateLimiter(
            NullLogger<SugarGuard.Bot.Services.TelegramRateLimiter>.Instance, perMinuteLimit: limit);
    }

    [Fact]
    public void TryAcquire_FirstCall_ReturnsTrue()
    {
        var limiter = CreateLimiter(limit: 5);
        Assert.True(limiter.TryAcquire(userId: 1001));
    }

    [Fact]
    public void TryAcquire_UnderLimit_AllAllowed()
    {
        var limiter = CreateLimiter(limit: 5);
        for (int i = 0; i < 5; i++)
        {
            Assert.True(limiter.TryAcquire(userId: 1002), $"call #{i + 1} should be allowed");
        }
    }

    [Fact]
    public void TryAcquire_ExceedsLimit_ReturnsFalse()
    {
        var limiter = CreateLimiter(limit: 3);
        Assert.True(limiter.TryAcquire(userId: 1003));
        Assert.True(limiter.TryAcquire(userId: 1003));
        Assert.True(limiter.TryAcquire(userId: 1003));
        Assert.False(limiter.TryAcquire(userId: 1003));
        Assert.False(limiter.TryAcquire(userId: 1003));
    }

    [Fact]
    public void TryAcquire_DifferentUsers_IndependentBuckets()
    {
        var limiter = CreateLimiter(limit: 2);
        Assert.True(limiter.TryAcquire(userId: 2001));
        Assert.True(limiter.TryAcquire(userId: 2001));
        Assert.False(limiter.TryAcquire(userId: 2001));

        // Другой пользователь — своё ведро.
        Assert.True(limiter.TryAcquire(userId: 2002));
        Assert.True(limiter.TryAcquire(userId: 2002));
        Assert.False(limiter.TryAcquire(userId: 2002));
    }

    [Fact]
    public void TryAcquire_ConcurrencySafe_NoRaceCondition()
    {
        var limiter = CreateLimiter(limit: 100);
        const int userId = 3001;
        const int attempts = 1000;

        var successCount = 0;
        var lockObj = new object();

        Parallel.For(0, attempts, _ =>
        {
            if (limiter.TryAcquire(userId))
            {
                lock (lockObj)
                    successCount++;
            }
        });

        // Параллельно из 1000 попыток только первые 100 должны быть успешны.
        // (Interlocked.Increment гарантирует atomic, поэтому число строго = 100.)
        Assert.Equal(100, successCount);
    }
}
