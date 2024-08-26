using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class AppSettingsWritableOptions
    {
        IWritableOptions<FileLoggerOptions> _loggerOptions;
        IWritableOptions<HttpClientOptions> _clientOptions;
        IWritableOptions<WatchdogOptions> _watchDogOptions;
        public AppSettingsWritableOptions(IWritableOptions<FileLoggerOptions> loggerOptions,
           IWritableOptions<HttpClientOptions> clientOptions,
           IWritableOptions<WatchdogOptions> watchDogOptions)
        { 
            _loggerOptions = loggerOptions;
            _clientOptions = clientOptions;
            _watchDogOptions = watchDogOptions;
        }

        public IWritableOptions<FileLoggerOptions> LPSFileLoggerOptions { get { return _loggerOptions; } }
        public IWritableOptions<HttpClientOptions> LPSHttpClientOptions { get { return _clientOptions; } }
        public IWritableOptions<WatchdogOptions> LPSWatchdogOptions { get { return _watchDogOptions; } }
    }
}
