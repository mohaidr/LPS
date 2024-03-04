using LPS.Domain;
using LPS.Domain.Common.Interfaces;
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
        ILPSMonitoringEnroller _lpsMonitoringEnroller;
        internal LPSManager(ILPSLogger logger,
                ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse,ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClientManager,
                ILPSClientConfiguration<LPSHttpRequestProfile> config,
                ILPSWatchdog wtahcdog,
                ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
                ILPSMonitoringEnroller lpsMonitoringEnroller)
        {
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _watchdog = wtahcdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _lpsMonitoringEnroller = lpsMonitoringEnroller;
        }
        public async Task Run(LPSTestPlan lpsPlan, CancellationToken cancellationToken)
        {
            CancellationTokenWrapper cancellationTokenWrapper = new CancellationTokenWrapper(cancellationToken);
            if (lpsPlan!=null && lpsPlan.IsValid && lpsPlan.LPSHttpRuns.Count>0)
            {
                LPSServer.Initialize(_logger, _runtimeOperationIdProvider);
                _ = LPSServer.Run(cancellationTokenWrapper);
                LPSDashboard.Start();
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan '{lpsPlan.Name}' execution has started", LPSLoggingLevel.Information);
                await new LPSTestPlan.ExecuteCommand(_logger, _watchdog, _runtimeOperationIdProvider, _httpClientManager, _config, _lpsMonitoringEnroller)
                    .ExecuteAsync(lpsPlan, cancellationTokenWrapper);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan '{lpsPlan.Name}' execution has completed", LPSLoggingLevel.Information);
            }
        }

    }
}
