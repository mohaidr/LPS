using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTest.Domain.Common
{

    public enum LoggingLevel
    {
        Informational,
        Warning,
        Error
    }
    public interface ICustomLogger
    {
        public string Location { get; set; }
        void Log(string EventId, string DiagnosticMessage, LoggingLevel Level);
        Task LogAsync(string EventId, string DiagnosticMessage, LoggingLevel Level);
    }
}
