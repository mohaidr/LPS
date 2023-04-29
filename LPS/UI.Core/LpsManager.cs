using LPS.Domain;
using LPS.Domain.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core
{
    internal class LpsManager
    {
        private ICustomLogger _logger;
        ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> _httpClientManager;
        ILPSClientConfiguration<LPSHttpRequest> _config;
        internal LpsManager(ICustomLogger logger,
                ILPSClientManager<LPSHttpRequest,
                ILPSClientService<LPSHttpRequest>> httpClientManager,
                ILPSClientConfiguration<LPSHttpRequest> config)
        {
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
        }
        private bool _endWatching;
        public async Task Run(LPSTestPlan.SetupCommand planCommand)
        {
            if (planCommand.IsValid)
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                var cancellationTask = WatchForCancellation(cts);
                await _logger.LogAsync("0000-0000-0000", "New Test Has Been Started", LoggingLevel.INF);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("...Test has started...");
                Console.ResetColor();

                var lpsTest = new LPSTestPlan(planCommand, _httpClientManager, _config, _logger);
                await new LPSTestPlan.ExecuteCommand().ExecuteAsync(lpsTest, cts.Token);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"...Your Performance Test Plan {planCommand.Name} Has Executed Successfully...");
                Console.ResetColor();
                await _logger.LogAsync("0000-0000-0000", "Test Has Completed", LoggingLevel.INF);
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
