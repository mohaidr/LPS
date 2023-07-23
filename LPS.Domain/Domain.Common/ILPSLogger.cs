using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common
{

    public enum LPSLoggingLevel
    {
        Verbos,
        Information,
        Warning,
        Error,
        Critical,
    }
    public interface ILPSLogger
    {
        public bool EnableConsoleLogging { get; set; }
        public LPSLoggingLevel ConsoleLoggingLevel { get; set; }
        public LPSLoggingLevel LoggingLevel { get; set; }
        void Log(string EventId, string DiagnosticMessage, LPSLoggingLevel Level, CancellationToken cancellationToken = default);
        Task LogAsync(string EventId, string DiagnosticMessage, LPSLoggingLevel Level, CancellationToken cancellationToken = default);
        public Task Flush();
    }
}
