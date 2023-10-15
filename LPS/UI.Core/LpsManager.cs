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
        ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> _httpClientManager;
        ILPSClientConfiguration<LPSHttpRequest> _config;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSResourceTracker _resourceUsageTracker;
        internal LPSManager(ILPSLogger logger,
                ILPSClientManager<LPSHttpRequest,
                ILPSClientService<LPSHttpRequest>> httpClientManager,
                ILPSClientConfiguration<LPSHttpRequest> config,
                ILPSResourceTracker resourceUsageTracker,
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _resourceUsageTracker = resourceUsageTracker;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }
        public async Task Run(LPSTestPlan.SetupCommand planCommand, CancellationToken cancellationToken)
        {
            var lpsTest = new LPSTestPlan(planCommand, _httpClientManager, _config, _logger, _resourceUsageTracker, _runtimeOperationIdProvider);
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
