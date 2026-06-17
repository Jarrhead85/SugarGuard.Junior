using SugarGuard.API.Application.Services;

namespace SugarGuard.Tests.Application.Services;

/// <summary>
/// Тесты для <see cref="GigaChatTokenCache"/>.
/// <para>
/// <b>M-4 (release 1.0.0):</b> negative cache TTL.
/// Проверяем что при null-ответе фабрики:
/// <list type="number">
///   <item><description>фабрика НЕ вызывается повторно в течение NegativeCacheTtl</description></item>
///   <item><description>после истечения NegativeCacheTtl фабрика вызывается снова</description></item>
///   <item><description>valid token по-прежнему кэшируется на полный TokenTtl (25 мин)</description></item>
///   <item><description>параллельные вызовы не приводят к race condition</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Решает:</b> M-4 (release 1.0.0) — DoS-вектор при GigaChat downtime.
/// </para>
/// </summary>
public class GigaChatTokenCacheTests
{
    /// <summary>
    /// M-4: если фабрика вернула null, второй вызов в течение
    /// negative-cache TTL НЕ должен вызывать фабрику повторно.
    /// </summary>
    [Fact]
    public async Task GetOrRefreshAsync_FactoryReturnsNull_NegativeCacheSkipsSecondCall()
    {
        var cache = new GigaChatTokenCache();
        var factoryCallCount = 0;

        Func<Task<string?>> failingFactory = () =>
        {
            factoryCallCount++;
            return Task.FromResult<string?>(null);
        };

        // Первый вызов — фабрика вызывается, возвращает null
        var first = await cache.GetOrRefreshAsync(failingFactory);
        Assert.Null(first);
        Assert.Equal(1, factoryCallCount);

        // Второй вызов сразу же — фабрика НЕ должна вызываться (negative cache)
        var second = await cache.GetOrRefreshAsync(failingFactory);
        Assert.Null(second);
        Assert.Equal(1, factoryCallCount); // <-- ключевая проверка M-4
    }

    /// <summary>
    /// Valid token кэшируется на полный TokenTtl (25 мин).
    /// </summary>
    [Fact]
    public async Task GetOrRefreshAsync_FactoryReturnsValidToken_CachesForFullTtl()
    {
        var cache = new GigaChatTokenCache();
        var factoryCallCount = 0;

        Func<Task<string?>> validFactory = () =>
        {
            factoryCallCount++;
            return Task.FromResult<string?>("valid-token-abc");
        };

        // Первый вызов
        var first = await cache.GetOrRefreshAsync(validFactory);
        Assert.Equal("valid-token-abc", first);
        Assert.Equal(1, factoryCallCount);

        // Сразу же — фабрика НЕ вызывается
        var second = await cache.GetOrRefreshAsync(validFactory);
        Assert.Equal("valid-token-abc", second);
        Assert.Equal(1, factoryCallCount);
    }

    /// <summary>
    /// Параллельные вызовы: SemaphoreSlim обеспечивает single-flight.
    /// </summary>
    [Fact]
    public async Task GetOrRefreshAsync_ParallelCalls_FactoryCalledOnce()
    {
        var cache = new GigaChatTokenCache();
        var factoryCallCount = 0;
        var factoryStarted = new TaskCompletionSource<bool>();

        Func<Task<string?>> slowFactory = async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            factoryStarted.SetResult(true);
            // Имитируем медленный HTTP-запрос
            await Task.Delay(100);
            return "slow-token";
        };

        // Запускаем 5 параллельных вызовов
        var task1 = Task.Run(() => cache.GetOrRefreshAsync(slowFactory));
        var task2 = Task.Run(() => cache.GetOrRefreshAsync(slowFactory));
        var task3 = Task.Run(() => cache.GetOrRefreshAsync(slowFactory));
        var task4 = Task.Run(() => cache.GetOrRefreshAsync(slowFactory));
        var task5 = Task.Run(() => cache.GetOrRefreshAsync(slowFactory));

        var results = await Task.WhenAll(task1, task2, task3, task4, task5);

        // Все должны вернуть один и тот же токен
        Assert.All(results, r => Assert.Equal("slow-token", r));
        // Фабрика вызвана ОДИН раз благодаря SemaphoreSlim
        Assert.Equal(1, factoryCallCount);
    }

    /// <summary>
    /// M-4: после истечения negative-cache TTL фабрика вызывается снова
    /// (cache не отравлен навечно).
    /// </summary>
    [Fact]
    public async Task GetOrRefreshAsync_NegativeCacheExpires_FactoryCalledAgain()
    {
        // Используем reflection чтобы выставить короткий NegativeCacheTtl для теста.
        // NegativeCacheTtl = TimeSpan.FromMinutes(1) — это слишком долго для теста,
        // поэтому вручную "обнуляем" _expiry через reflection.
        var cache = new GigaChatTokenCache();
        var factoryCallCount = 0;

        Func<Task<string?>> factory = () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return Task.FromResult<string?>(null);
        };

        // Первый вызов — фабрика вызвана
        await cache.GetOrRefreshAsync(factory);
        Assert.Equal(1, factoryCallCount);

        // Симулируем истечение negative cache: сдвигаем _expiry в прошлое
        var expiryField = typeof(GigaChatTokenCache)
            .GetField("_expiry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(expiryField);
        expiryField!.SetValue(cache, DateTime.UtcNow.AddSeconds(-1));

        // Теперь фабрика должна вызваться снова
        await cache.GetOrRefreshAsync(factory);
        Assert.Equal(2, factoryCallCount);
    }

    /// <summary>
    /// M-4: после успешного токена null-фабрика сбрасывает кэш,
    /// НО valid-токен после null перезаписывает кэш.
    /// </summary>
    [Fact]
    public async Task GetOrRefreshAsync_AfterNull_ValidToken_OverwritesCache()
    {
        var cache = new GigaChatTokenCache();
        var phase = 0; // 0 = null, 1 = valid

        Func<Task<string?>> phaseFactory = () =>
        {
            return Task.FromResult<string?>(phase == 0 ? null : "recovered-token");
        };

        // Фаза 0: null
        phase = 0;
        var first = await cache.GetOrRefreshAsync(phaseFactory);
        Assert.Null(first);

        // Negative cache блокирует — фабрика не вызывается
        var second = await cache.GetOrRefreshAsync(phaseFactory);
        Assert.Null(second);

        // Сдвигаем negative cache, переходим в фазу 1
        var expiryField = typeof(GigaChatTokenCache)
            .GetField("_expiry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        expiryField!.SetValue(cache, DateTime.UtcNow.AddSeconds(-1));
        phase = 1;

        // Фаза 1: valid token
        var third = await cache.GetOrRefreshAsync(phaseFactory);
        Assert.Equal("recovered-token", third);

        // Кэшируется
        var fourth = await cache.GetOrRefreshAsync(phaseFactory);
        Assert.Equal("recovered-token", fourth);
    }
}
