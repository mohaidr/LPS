using LPS.Domain;
using LPS.Domain.Common;
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
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSWatchdog _watchdog;
        internal LPSManager(ILPSLogger logger,
                ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse,
                ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClientManager,
                ILPSClientConfiguration<LPSHttpRequestProfile> config,
                ILPSWatchdog wtahcdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _watchdog = wtahcdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }
        public async Task Run(LPSTestPlan.SetupCommand planCommand, CancellationToken cancellationToken)
        {
            var lpsTest = new LPSTestPlan(planCommand, _httpClientManager, _config, _logger, _watchdog, _runtimeOperationIdProvider);
            CancellationTokenWrapper cancellationTokenWrapper = new CancellationTokenWrapper(cancellationToken);
            if (lpsTest.IsValid)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan '{planCommand.Name}' execution has started", LPSLoggingLevel.Information);
                await new LPSTestPlan.ExecuteCommand().ExecuteAsync(lpsTest, cancellationTokenWrapper);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan '{planCommand.Name}' execution has completed", LPSLoggingLevel.Information);
            }
        }

    }
}
