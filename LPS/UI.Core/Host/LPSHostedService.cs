using LPS.UI.Common;
using System.Threading;
using System.Threading.Tasks;
using LPS.UI.Core.UI.Build.Services;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using System.IO;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSValidators;
using LPS.Infrastructure.Common;
using Spectre.Console;
using LPS.UI.Core.LPSCommandLine;

namespace LPS.UI.Core.Host
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
        string[] _command_args;
        public LPSHostedService(
            dynamic command_args,
            ILPSLogger logger,
            ILPSClientConfiguration<LPSHttpRequestProfile> config,
            ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClientManager,
            ILPSWatchdog watchdog,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILPSMonitoringEnroller lPSMonitoringEnroller,
            LPSAppSettingsWritableOptions appSettings)
        {
            _logger = logger;
            _config = config;
            _command_args = command_args.args;
            _httpClientManager = httpClientManager;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _appSettings = appSettings;
            _lPSMonitoringEnroller = lPSMonitoringEnroller;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has started  --------------", LPSLoggingLevel.Verbose);
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"is the correlation Id of this run", LPSLoggingLevel.Information);
            LPSTestPlan.SetupCommand lpsTestPlanSetupCommand = new LPSTestPlan.SetupCommand();

            if (_command_args != null && _command_args.Length > 0)
            {
                var commandLineManager = new LPSCommandLineManager(_command_args, _logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _appSettings, lpsTestPlanSetupCommand, _lPSMonitoringEnroller);
                commandLineManager.Run(cancellationToken);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Command execution has completed", LPSLoggingLevel.Verbose);
            }
            else
            {
                var manualBuild = new ManualBuild(new LPSTestPlanValidator(lpsTestPlanSetupCommand), _logger, _runtimeOperationIdProvider);
                var lpsRun = manualBuild.Build(lpsTestPlanSetupCommand);
                File.WriteAllText($"{lpsTestPlanSetupCommand.Name}.json", LPSSerializationHelper.Serialize(lpsTestPlanSetupCommand));


                bool runTest = AnsiConsole.Confirm("Would you like to run your test now?");
                if (runTest)
                {
                    var lpsManager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _lPSMonitoringEnroller);
                    await lpsManager.RunAsync(lpsRun, cancellationToken);
                }

                AnsiConsole.MarkupLine($"[bold italic]You can use the command [blue]lps run -tn {lpsTestPlanSetupCommand.Name}[/] to execute the plan[/]");
            }
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has completed  --------------", LPSLoggingLevel.Verbose);

            await _logger.Flush();
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _logger?.LogAsync(_runtimeOperationIdProvider.OperationId, "--------------  LPS V1 - App Exited  --------------", LPSLoggingLevel.Verbose);
            await _logger?.Flush();
        }
    }
}
