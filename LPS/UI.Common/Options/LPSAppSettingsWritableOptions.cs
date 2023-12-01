using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class LPSAppSettingsWritableOptions
    {
        IWritableOptions<LPSFileLoggerOptions> _loggerOptions;
        IWritableOptions<LPSHttpClientOptions> _clientOptions;
        IWritableOptions<LPSWatchdogOptions> _watchDogOptions;
        public LPSAppSettingsWritableOptions(IWritableOptions<LPSFileLoggerOptions> loggerOptions,
           IWritableOptions<LPSHttpClientOptions> clientOptions,
           IWritableOptions<LPSWatchdogOptions> watchDogOptions)
        { 
            _loggerOptions = loggerOptions;
            _clientOptions = clientOptions;
            _watchDogOptions = watchDogOptions;
        }

        public IWritableOptions<LPSFileLoggerOptions> LPSFileLoggerOptions { get { return _loggerOptions; } }
        public IWritableOptions<LPSHttpClientOptions> LPSHttpClientOptions { get { return _clientOptions; } }
        public IWritableOptions<LPSWatchdogOptions> LPSWatchdogOptions { get { return _watchDogOptions; } }
    }
}
