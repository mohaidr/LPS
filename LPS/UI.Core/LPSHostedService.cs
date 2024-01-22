using LPS.UI.Common;
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.UI.Core.UI.Build.Services;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using System.IO;
using LPS.UI.Core.LPSCommandLine.Commands;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSValidators;
using LPS.Infrastructure.Common;

namespace LPS.UI.Core
{
    internal class LPSHostedService : ILPSHostedService
    {
        ILPSLogger _logger;
        ILPSClientConfiguration<LPSHttpRequestProfile> _config;
        ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> _httpClientManager;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSWatchdog _watchdog;
        LPSAppSettingsWritableOptions _appSettings;
        ILPSMonitoringEnroller _lPSMonitoringEnroller;
        string[] _args;
        public LPSHostedService(ILPSLogger logger,
            ILPSClientConfiguration<LPSHttpRequestProfile> config,
            ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClientManager,
            ILPSWatchdog watchdog,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILPSMonitoringEnroller lPSMonitoringEnroller,
            dynamic cmdArgs, 
            LPSAppSettingsWritableOptions appSettings)
        {
            _logger = logger;
            _config = config;
            _args = cmdArgs.args;
            _httpClientManager = httpClientManager;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _appSettings = appSettings;
            _lPSMonitoringEnroller = lPSMonitoringEnroller;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has started  --------------", LPSLoggingLevel.Information);
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"is the correlation Id of this run", LPSLoggingLevel.Information);
            LPSTestPlan.SetupCommand lpsTestPlanSetupCommand = new LPSTestPlan.SetupCommand();

            if (_args != null && _args.Length > 0)
            {
                var commandLineParser = new LPSRootCLICommand(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _appSettings, lpsTestPlanSetupCommand, _lPSMonitoringEnroller, _args);
                commandLineParser.Execute(cancellationToken);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Command execution has completed", LPSLoggingLevel.Information);
            }
            else
            {
                var manualBuild = new ManualBuild(new LPSTestPlanValidator(lpsTestPlanSetupCommand), _logger, _runtimeOperationIdProvider);
                var lpsRun = manualBuild.Build(lpsTestPlanSetupCommand);
                File.WriteAllText($"{lpsTestPlanSetupCommand.Name}.json", LPSSerializationHelper.Serialize(lpsTestPlanSetupCommand));

                Console.WriteLine("Enter (Y) if you want to run the test or (N) if you want to run the test through commmands later");
                bool runTest = Console.ReadLine().Equals("y", StringComparison.OrdinalIgnoreCase);
                if (runTest)
                {
                    var lpsManager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _lPSMonitoringEnroller);
                    await lpsManager.Run(lpsRun, cancellationToken);
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
