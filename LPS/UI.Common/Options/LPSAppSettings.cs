using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class LPSAppSettings
    {
        public LPSHttpClientOptions LPSHttpClientConfiguration { get; set; }
        public LPSFileLoggerOptions LPSFileLoggerConfiguration { get; set;}
        public LPSWatchdogOptions LPSWatchdogConfiguration { get; set; }
    }
}
