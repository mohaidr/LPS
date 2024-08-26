using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{

    public enum LPSLoggingLevel
    {
        Verbose,
        Information,
        Warning,
        Error,
        Critical,
    }
    public interface ILogger
    {
        void Log(string EventId, string DiagnosticMessage, LPSLoggingLevel Level, CancellationTokenSource cts = default);
        Task LogAsync(string EventId, string DiagnosticMessage, LPSLoggingLevel Level, CancellationTokenSource cts = default);
        public Task Flush();
    }
}
