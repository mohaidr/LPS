using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.UI.Core.Host;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core
{
    internal class LPSManager
    {
        readonly ILogger _logger;
        readonly IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager;
        readonly IClientConfiguration<HttpRequestProfile> _config;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IWatchdog _watchdog;
        readonly IMetricsDataMonitor _lpsMonitoringEnroller;
        readonly ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor;
        readonly CancellationTokenSource _cts;
        internal LPSManager(ILogger logger,
                IClientManager<HttpRequestProfile, HttpResponse,IClientService<HttpRequestProfile, HttpResponse>> httpClientManager,
                IClientConfiguration<HttpRequestProfile> config,
                IWatchdog wtahcdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunExecutionCommandStatusMonitor,
                IMetricsDataMonitor lpsMonitoringEnroller, 
                CancellationTokenSource cts)
        {
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _watchdog = wtahcdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
            _lpsMonitoringEnroller = lpsMonitoringEnroller;
            _cts = cts;
        }
        public async Task RunAsync(TestPlan lpsPlan)
        {
            if (lpsPlan!=null && lpsPlan.IsValid && lpsPlan.LPSRuns.Count>0)
            {

                Host.Dashboard.Start();
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan '{lpsPlan.Name}' execution has started", LPSLoggingLevel.Information);
                await new TestPlan.ExecuteCommand(_logger, _watchdog, _runtimeOperationIdProvider, _httpClientManager, _config, _httpRunExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _cts)
                    .ExecuteAsync(lpsPlan);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan '{lpsPlan.Name}' execution has completed", LPSLoggingLevel.Information);
            }
        }

    }
}
