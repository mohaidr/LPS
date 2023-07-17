using LPS.Domain;
using LPS.Domain.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core
{
    internal class LpsManager
    {
        private ILPSLogger _logger;
        ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> _httpClientManager;
        ILPSClientConfiguration<LPSHttpRequest> _config;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        internal LpsManager(ILPSLogger logger,
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
        private bool _endWatching;
        public async Task Run(LPSTestPlan.SetupCommand planCommand)
        {
            if (planCommand.IsValid)
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                var cancellationTask = WatchForCancellation(cts);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Test has started", LPSLoggingLevel.Information);

                var lpsTest = new LPSTestPlan(planCommand, _httpClientManager, _config, _logger, _runtimeOperationIdProvider);
                await new LPSTestPlan.ExecuteCommand().ExecuteAsync(lpsTest, cts.Token);

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Test plan '{planCommand.Name}' execution has completed", LPSLoggingLevel.Information);
                _endWatching = true;
            }
        }

        public async Task WatchForCancellation(CancellationTokenSource cts)
        {
            while (!_endWatching)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                    if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        cts.Cancel();
                    }
                }
                await Task.Delay(1000);
            }
        }
    }
}
