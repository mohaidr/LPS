using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class AppSettingsWritableOptions
    {
        readonly IWritableOptions<FileLoggerOptions> _loggerOptions;
        readonly IWritableOptions<HttpClientOptions> _clientOptions;
        readonly IWritableOptions<WatchdogOptions> _watchDogOptions;
        readonly IWritableOptions<ClusterConfigurationOptions> _clusterOptions;
        readonly IWritableOptions<DashboardConfigurationOptions> _dashboardConfigurationOptions;
        public AppSettingsWritableOptions(IWritableOptions<FileLoggerOptions> loggerOptions,
           IWritableOptions<HttpClientOptions> clientOptions,
           IWritableOptions<WatchdogOptions> watchDogOptions,
           IWritableOptions<DashboardConfigurationOptions> dashboardConfigurationOptions,
           IWritableOptions<ClusterConfigurationOptions> clusterOptions)
        { 
            _loggerOptions = loggerOptions;
            _clientOptions = clientOptions;
            _watchDogOptions = watchDogOptions;
            _dashboardConfigurationOptions = dashboardConfigurationOptions;
            _clusterOptions = clusterOptions;
        }

        public IWritableOptions<FileLoggerOptions> FileLoggerOptions { get { return _loggerOptions; } }
        public IWritableOptions<HttpClientOptions> HttpClientOptions { get { return _clientOptions; } }
        public IWritableOptions<WatchdogOptions> WatchdogOptions { get { return _watchDogOptions; } }
        public IWritableOptions<ClusterConfigurationOptions> ClusterConfigurationOptions { get { return _clusterOptions; } }
        public IWritableOptions<DashboardConfigurationOptions> DashboardConfigurationOptions { get { return _dashboardConfigurationOptions; } }
    }
}
