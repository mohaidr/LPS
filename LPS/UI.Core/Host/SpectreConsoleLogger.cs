using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LPS.UI.Core.Host
{
    internal sealed class SpectreConsoleLogger(string categoryName, SpectreConsoleLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => provider.ScopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                provider.Write(new LogEntry<TState>(logLevel, categoryName, eventId, state, exception, formatter));
        }
    }
}