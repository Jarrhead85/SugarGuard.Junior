using System.Globalization;
using System.Text;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Файловое хранилище диагностических логов мобильного приложения.
/// </summary>
public sealed class FileAppDiagnosticsLogService : IAppDiagnosticsLogService
{
    private const long MaxLogFileBytes = 1024 * 1024;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _logPath = Path.Combine(FileSystem.AppDataDirectory, "sugarguard-mobile.log");

    /// <inheritdoc/>
    public async Task AppendAsync(
        string category,
        string level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        var safeMessage = Normalize(message);
        var exceptionText = exception is null
            ? string.Empty
            : $" | {exception.GetType().Name}: {Normalize(exception.Message)}";
        var line = $"{DateTimeOffset.UtcNow:O}\t{level}\t{Normalize(category)}\t{safeMessage}{exceptionText}{Environment.NewLine}";

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_logPath, line, Encoding.UTF8, cancellationToken);
            await TrimIfNeededAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> ReadRecentAsync(TimeSpan period, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_logPath))
        {
            return "Логи за последний час отсутствуют.";
        }

        var threshold = DateTimeOffset.UtcNow.Subtract(period);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var lines = await File.ReadAllLinesAsync(_logPath, Encoding.UTF8, cancellationToken);
            var recent = lines.Where(line => IsRecent(line, threshold)).TakeLast(500).ToArray();
            return recent.Length == 0
                ? "Логи за последний час отсутствуют."
                : string.Join(Environment.NewLine, recent);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task TrimIfNeededAsync(CancellationToken cancellationToken)
    {
        var file = new FileInfo(_logPath);
        if (!file.Exists || file.Length <= MaxLogFileBytes)
        {
            return;
        }

        var lines = await File.ReadAllLinesAsync(_logPath, Encoding.UTF8, cancellationToken);
        var tail = lines.TakeLast(Math.Max(200, lines.Length / 2));
        await File.WriteAllLinesAsync(_logPath, tail, Encoding.UTF8, cancellationToken);
    }

    private static bool IsRecent(string line, DateTimeOffset threshold)
    {
        var tabIndex = line.IndexOf('\t', StringComparison.Ordinal);
        if (tabIndex <= 0)
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            line[..tabIndex],
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var timestamp)
            && timestamp >= threshold;
    }

    private static string Normalize(string value) =>
        value.ReplaceLineEndings(" ").Trim();
}
