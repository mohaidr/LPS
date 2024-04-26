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
    public interface ILPSLogger
    {
        void Log(string EventId, string DiagnosticMessage, LPSLoggingLevel Level, ICancellationTokenWrapper cancellationTokenWrapper = default);
        Task LogAsync(string EventId, string DiagnosticMessage, LPSLoggingLevel Level, ICancellationTokenWrapper cancellationTokenWrapper = default);
        public Task Flush();
    }
}
