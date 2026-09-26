using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace LPS.UI.Core.Host
{
    [ProviderAlias("Console")]
    internal sealed class SpectreConsoleLoggerProvider(
        SpectreConsoleFormatter formatter,
        ILiveConsoleOutput output,
        IOptionsMonitor<ConsoleLoggerOptions> options) : ILoggerProvider, ISupportExternalScope
    {
        internal IExternalScopeProvider ScopeProvider { get; private set; } = new LoggerExternalScopeProvider();

        public ILogger CreateLogger(string categoryName) => new SpectreConsoleLogger(categoryName, this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => ScopeProvider = scopeProvider;

        internal void Write<TState>(in LogEntry<TState> entry)
        {
            var message = formatter.Format(entry, ScopeProvider);
            if (message != null)
                output.Write(message, entry.LogLevel >= options.CurrentValue.LogToStandardErrorThreshold);
        }

        public void Dispose() { }
    }
}