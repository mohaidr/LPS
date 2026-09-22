using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Spectre.Console;
using System.Globalization;
using System.IO;

namespace LPS.UI.Core.Host
{
    internal sealed class SpectreConsoleFormatter(
        ILiveConsoleOutput output,
        IOptionsMonitor<SimpleConsoleFormatterOptions> options) : ConsoleFormatter("spectre")
    {
        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            if (string.IsNullOrEmpty(message) && logEntry.Exception == null)
            {
                return;
            }

            var settings = options.CurrentValue;
            var (level, color) = logEntry.LogLevel switch
            {
                LogLevel.Trace => ("trce", Color.Grey),
                LogLevel.Debug => ("dbug", Color.Grey),
                LogLevel.Information => ("info", Color.Green),
                LogLevel.Warning => ("warn", Color.Yellow),
                LogLevel.Error => ("fail", Color.Red),
                LogLevel.Critical => ("crit", Color.Red),
                _ => ("none", Color.Grey)
            };
            var text = new Paragraph();
            if (settings.TimestampFormat != null)
            {
                var now = settings.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
                text.Append(now.ToString(settings.TimestampFormat, CultureInfo.InvariantCulture));
            }

            text.Append($"{level}:", settings.ColorBehavior == LoggerColorBehavior.Disabled ? Style.Plain : new Style(color));
            text.Append($" {logEntry.Category}[{logEntry.EventId.Id}]");
            var separator = settings.SingleLine ? " " : Environment.NewLine + "      ";
            if (settings.IncludeScopes && scopeProvider != null)
            {
                scopeProvider.ForEachScope((scope, output) => output.Append($"{separator}=> {scope}"), text);
            }
            if (!string.IsNullOrEmpty(message))
            {
                text.Append(separator + message.ReplaceLineEndings(separator));
            }
            if (logEntry.Exception != null)
            {
                text.Append(separator + logEntry.Exception.ToString().ReplaceLineEndings(separator));
            }
            text.Append(Environment.NewLine);
            output.Write(text);
        }
    }
}