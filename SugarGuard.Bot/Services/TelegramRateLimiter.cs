using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SugarGuard.Bot.Services;

/// <summary>
/// Per-user rate limiter для Telegram-бота.
/// <para>
/// <b>Проблема (фикс 2026-06-03):</b> до этого фикса у бота не было per-user rate limit.
/// Атакующий мог:
/// <list type="bullet">
///   <item><description>Брутфорсить код `/connect ABCD-1234` (24 бита = 16M комбинаций, но
///     за тысячи попыток с одного аккаунта — реалистично).</description></item>
///   <item><description>Спамить "Add snack" / "Statistics" → нагрузка на API.</description></item>
///   <item><description>Спамить "Statistics" → дорогой <c>ExportToPdfAsync</c> × N раз.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Реализация:</b> token bucket через <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// с lock-free <see cref="Interlocked.Increment(ref int)"/>. На каждого
/// <c>userId</c> хранится счётчик + временное окно. При превышении лимита
/// возвращается <c>false</c>, handler показывает пользователю сообщение
/// "Слишком много запросов".
/// </para>
/// <para>
/// <b>Concurrency:</b> <see cref="ConcurrentDictionary{TKey, TValue}.GetOrAdd"/> атомарен,
/// <see cref="Interlocked.Increment(ref int)"/> — lock-free.
/// </para>
/// <para>
/// заменил <c>IMemoryCache.GetOrCreate</c> на
/// <c>ConcurrentDictionary.GetOrAdd</c>: первый делал race при первом обращении
/// (создавал разные счётчики в concurrent потоках), второй — атомарен.
/// </para>
/// </summary>
public sealed class TelegramRateLimiter
{
    /// <summary>Лимит сообщений на пользователя в минуту.</summary>
    private const int DefaultPerMinuteLimit = 20;

    /// <summary>TTL для счётчика (sliding window = 1 минута).</summary>
    private static readonly TimeSpan WindowDuration = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<long, RateLimitCounter> _counters = new();
    private readonly ILogger<TelegramRateLimiter> _logger;
    private readonly int _perMinuteLimit;

    public TelegramRateLimiter(
        ILogger<TelegramRateLimiter> logger,
        int perMinuteLimit = DefaultPerMinuteLimit)
    {
        _logger = logger;
        _perMinuteLimit = perMinuteLimit;
    }

    /// <summary>
    /// Проверяет, может ли пользователь выполнить ещё один запрос.
    /// Увеличивает счётчик атомарно. Возвращает <c>true</c>, если запрос разрешён.
    /// </summary>
    public bool TryAcquire(long userId)
    {
        // GetOrAdd атомарен: если counter отсутствует, создаётся новый
        // и тот же instance возвращается всем concurrent reader'ам.
        var counter = _counters.GetOrAdd(userId, _ => new RateLimitCounter { WindowStartedAt = DateTime.UtcNow });

        // Interlocked — lock-free atomic increment.
        var newCount = Interlocked.Increment(ref counter.Count);

        // TTL eviction: если окно истекло — сбрасываем счётчик.
        if (DateTime.UtcNow - counter.WindowStartedAt > WindowDuration)
        {
            if (Interlocked.Exchange(ref counter.InReset, 1) == 0)
            {
                Interlocked.Exchange(ref counter.Count, 1); // новый запрос после сброса = 1
                counter.WindowStartedAt = DateTime.UtcNow;
                Interlocked.Exchange(ref counter.InReset, 0);
                _logger.LogDebug("Rate limit window expired for user {UserId}, reset.", userId);
            }
            return true; // первый запрос в новом окне — всегда разрешён
        }

        if (newCount > _perMinuteLimit)
        {
            // Логируем только при первом превышении в окне (не спамим на каждый rejected).
            if (newCount == _perMinuteLimit + 1)
            {
                _logger.LogWarning(
                    "Rate limit exceeded for user {UserId}: {Count}/{Limit} per {WindowMin}min.",
                    userId, newCount, _perMinuteLimit, WindowDuration.TotalMinutes);
            }
            return false;
        }

        return true;
    }

    /// <summary>
    /// Thread-safe счётчик для rate limiting. Хранится by-reference в
    /// <see cref="ConcurrentDictionary{TKey, TValue}"/>, поэтому
    /// <see cref="Interlocked.Increment(ref int)"/> атомарно обновляет значение
    /// для всех reader'ов того же key.
    /// </summary>
    private sealed class RateLimitCounter
    {
        public int Count;
        public DateTime WindowStartedAt;
        public int InReset; // 0/1 — spinlock для window reset
    }
}
