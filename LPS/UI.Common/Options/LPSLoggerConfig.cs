using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class LPSFileLoggerConfiguration
    {
        public string LogFilePath { get; set; }
        public LPSLoggingLevel ConsoleLogingLevel { get; set; }
        public bool EnableConsoleLogging { get; set; }
        public bool EnableConsoleErrorLogging { get; set; }
        public bool DisableFileLogging { get; set; }
        public LPSLoggingLevel LoggingLevel { get; set; }
    }
}
