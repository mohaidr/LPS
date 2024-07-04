using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.UI.Core.Host;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core
{
    internal class LPSManager
    {
        private ILPSLogger _logger;
        ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> _httpClientManager;
        ILPSClientConfiguration<LPSHttpRequestProfile> _config;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSWatchdog _watchdog;
        ILPSMetricsDataMonitor _lpsMonitoringEnroller;
        ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> _httpRunExecutionCommandStatusMonitor;
        internal LPSManager(ILPSLogger logger,
                ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse,ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClientManager,
                ILPSClientConfiguration<LPSHttpRequestProfile> config,
                ILPSWatchdog wtahcdog,
                ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
                ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> httpRunExecutionCommandStatusMonitor,
                ILPSMetricsDataMonitor lpsMonitoringEnroller)
        {
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _watchdog = wtahcdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
            _lpsMonitoringEnroller = lpsMonitoringEnroller;
        }
        public async Task RunAsync(LPSTestPlan lpsPlan, CancellationToken cancellationToken)
        {
            CancellationTokenWrapper cancellationTokenWrapper = new CancellationTokenWrapper(cancellationToken);
            if (lpsPlan!=null && lpsPlan.IsValid && lpsPlan.LPSRuns.Count>0)
            {
               
                LPSServer.Initialize(_logger, _httpRunExecutionCommandStatusMonitor, _runtimeOperationIdProvider, cancellationToken);
                _ = LPSServer.RunAsync(cancellationTokenWrapper);
                LPSDashboard.Start();
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan '{lpsPlan.Name}' execution has started", LPSLoggingLevel.Information);
                await new LPSTestPlan.ExecuteCommand(_logger, _watchdog, _runtimeOperationIdProvider, _httpClientManager, _config, _httpRunExecutionCommandStatusMonitor, _lpsMonitoringEnroller)
                    .ExecuteAsync(lpsPlan, cancellationTokenWrapper);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan '{lpsPlan.Name}' execution has completed", LPSLoggingLevel.Information);
            }
        }

    }
}
