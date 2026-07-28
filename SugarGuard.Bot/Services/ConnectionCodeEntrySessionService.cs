using System.Collections.Concurrent;

namespace SugarGuard.Bot.Services;

/// <summary>
/// Хранит короткоживущий сценарий ввода кода подключения.
/// Код не сохраняется: сервис помнит только, что следующее сообщение
/// пользователя нужно трактовать как код привязки.
/// </summary>
public sealed class ConnectionCodeEntrySessionService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<long, DateTimeOffset> _sessions = new();

    /// <summary>Начинает сценарий ввода кода для пользователя.</summary>
    public void Begin(long telegramUserId) =>
        _sessions[telegramUserId] = DateTimeOffset.UtcNow.Add(SessionLifetime);

    /// <summary>Проверяет, ожидается ли от пользователя код подключения.</summary>
    public bool IsAwaitingCode(long telegramUserId)
    {
        if (!_sessions.TryGetValue(telegramUserId, out var expiresAt))
        {
            return false;
        }

        if (expiresAt > DateTimeOffset.UtcNow)
        {
            return true;
        }

        _sessions.TryRemove(telegramUserId, out _);
        return false;
    }

    /// <summary>Завершает или отменяет сценарий ввода кода.</summary>
    public void Complete(long telegramUserId) => _sessions.TryRemove(telegramUserId, out _);
}
