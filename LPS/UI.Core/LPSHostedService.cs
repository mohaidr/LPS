using LPS.UI.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.UI.Core.UI.Build.Services;
using LPS.Domain;
using Microsoft.Extensions.Hosting;
using LPS.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.CompilerServices;

namespace LPS.UI.Core
{
    internal class LPSHostedService : ILPSHostedService
    {
        ILPSLogger _logger;
        ILPSClientConfiguration<LPSHttpRequest> _config;
        ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> _httpClientManager;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        string[] _args;
        public LPSHostedService(ILPSLogger logger,
            ILPSClientConfiguration<LPSHttpRequest> config,
            ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> httpClientManager,
            IRuntimeOperationIdProvider runtimeOperationIdProvider, 
            dynamic cmdArgs)
        {
            _logger = logger;
            _config = config;
            _args = cmdArgs.args;
            _httpClientManager = httpClientManager;
            _runtimeOperationIdProvider= runtimeOperationIdProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has started  --------------", LPSLoggingLevel.Information);  
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"is the correlation Id of this run", LPSLoggingLevel.Information);
            LPSTestPlan.SetupCommand lpsTestPlanSetupCommand = new LPSTestPlan.SetupCommand();

            if (_args != null && _args.Length > 0)
            {
                var commandLineParser = new LPSCommandParser(_logger, _httpClientManager, _config, _runtimeOperationIdProvider, lpsTestPlanSetupCommand);
                commandLineParser.CommandLineArgs = _args;
                commandLineParser.Parse(cancellationToken);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- Command execution has completed  --------------", LPSLoggingLevel.Information);
            }
            else
            {
                var manualBuild = new ManualBuild(new LPSTestPlanValidator(lpsTestPlanSetupCommand));
                manualBuild.Build(lpsTestPlanSetupCommand);
                File.WriteAllText($"{lpsTestPlanSetupCommand.Name}.json", new LpsSerializer().Serialize(lpsTestPlanSetupCommand));

                Console.WriteLine("Enter (Y) if you want to run the test or (N) if you want to run the test through commmands later");
                bool runTest = Console.ReadLine().ToLower().Trim() == "y";
                if (runTest)
                {
                    var lpsManager = new LPSManager(_logger, _httpClientManager, _config, _runtimeOperationIdProvider);
                    await lpsManager.Run(lpsTestPlanSetupCommand, cancellationToken);
                }

                Console.WriteLine($"You can use the command lps run -tn {lpsTestPlanSetupCommand.Name} to execute the plan");
            }
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has completed  --------------", LPSLoggingLevel.Information);

            await _logger.Flush();
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _logger?.LogAsync(_runtimeOperationIdProvider.OperationId, "--------------  LPS V1 - App Closed  --------------", LPSLoggingLevel.Information);
            await _logger?.Flush();
        }
    }
}
