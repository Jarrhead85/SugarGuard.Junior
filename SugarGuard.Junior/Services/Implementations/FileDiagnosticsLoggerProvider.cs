using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Провайдер логирования, который сохраняет последние диагностические события в файл приложения.
/// </summary>
public sealed class FileDiagnosticsLoggerProvider : ILoggerProvider
{
    private readonly IAppDiagnosticsLogService _diagnosticsLogService;

    /// <summary>
    /// Создаёт провайдер файлового диагностического логирования.
    /// </summary>
    public FileDiagnosticsLoggerProvider(IAppDiagnosticsLogService diagnosticsLogService)
    {
        _diagnosticsLogService = diagnosticsLogService;
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) =>
        new FileDiagnosticsLogger(categoryName, _diagnosticsLogService);

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private sealed class FileDiagnosticsLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IAppDiagnosticsLogService _diagnosticsLogService;

        public FileDiagnosticsLogger(string categoryName, IAppDiagnosticsLogService diagnosticsLogService)
        {
            _categoryName = categoryName;
            _diagnosticsLogService = diagnosticsLogService;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            _ = Task.Run(() => _diagnosticsLogService.AppendAsync(
                _categoryName,
                logLevel.ToString(),
                message,
                exception));
        }
    }
}
