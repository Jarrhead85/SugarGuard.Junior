namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Хранилище диагностических логов мобильного приложения.
/// </summary>
public interface IAppDiagnosticsLogService
{
    /// <summary>
    /// Записывает строку диагностического лога.
    /// </summary>
    Task AppendAsync(string category, string level, string message, Exception? exception = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает диагностические логи за указанный период.
    /// </summary>
    Task<string> ReadRecentAsync(TimeSpan period, CancellationToken cancellationToken = default);
}
