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
        internal LPSManager(ILPSLogger logger,
                ILPSClientManager<LPSHttpRequest,
                ILPSClientService<LPSHttpRequest>> httpClientManager,
                ILPSClientConfiguration<LPSHttpRequest> config,
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }
        public async Task Run(LPSTestPlan.SetupCommand planCommand, CancellationToken cancellationToken)
        {
            if (planCommand.IsValid)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Test has started", LPSLoggingLevel.Information);
                var lpsTest = new LPSTestPlan(planCommand, _httpClientManager, _config, _logger, _runtimeOperationIdProvider);
                await new LPSTestPlan.ExecuteCommand().ExecuteAsync(lpsTest, cancellationToken);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Test plan '{planCommand.Name}' execution has completed", LPSLoggingLevel.Information);
            }
        }

    }
}
