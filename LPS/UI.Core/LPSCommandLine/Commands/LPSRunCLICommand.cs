using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Monitoring;
using LPS.UI.Common;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class LPSRunCLICommand: ILPSCLICommand
    {
        Command _rootLpsCliCommand;
        LPSTestPlan.SetupCommand _planSetupCommand;
        private string[] _args;
        ILPSLogger _logger;
        ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> _httpClientManager;
        ILPSClientConfiguration<LPSHttpRequestProfile> _config;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSWatchdog _watchdog;
        Command _runCommand;
        ILPSMonitoringEnroller _lPSMonitoringEnroller;
        internal LPSRunCLICommand(Command rootCLICommandLine,
            LPSTestPlan.SetupCommand planSetupCommand,
            ILPSLogger logger,
            ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClientManager,
            ILPSClientConfiguration<LPSHttpRequestProfile> config,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILPSWatchdog watchdog,
            ILPSMonitoringEnroller lPSMonitoringEnroller,
            string[] args)
        {
            _rootLpsCliCommand = rootCLICommandLine;
            _planSetupCommand = planSetupCommand;
            _args = args;
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
            _lPSMonitoringEnroller = lPSMonitoringEnroller;
            Setup();
        }

        private void Setup()
        {

            _runCommand = new Command("run", "Run existing test");
            LPSCommandLineOptions.AddOptionsToCommand(_runCommand, typeof(LPSCommandLineOptions.LPSRunCommandOptions));
            _rootLpsCliCommand.AddCommand(_runCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _runCommand.SetHandler(async (testName) =>
            {
                try
                {
                    _planSetupCommand = LPSSerializationHelper.Deserialize<LPSTestPlan.SetupCommand>(File.ReadAllText($"{testName}.json"));
                    var lpsPlan = new LPSTestPlan(_planSetupCommand, _logger, _runtimeOperationIdProvider); // it should validate and throw if the command is not valid
                    foreach (var runCommand in _planSetupCommand.LPSHttpRuns)
                    {
                        var runEntity = new LPSHttpRun(runCommand, _logger, _runtimeOperationIdProvider); // must validate and throw if the command is not valid
                        var requestProfile = new LPSHttpRequestProfile(runCommand.LPSRequestProfile, _logger, _runtimeOperationIdProvider);
                        if (runEntity.IsValid && requestProfile.IsValid)
                        {
                            runEntity.LPSHttpRequestProfile = requestProfile;
                            lpsPlan.LPSHttpRuns.Add(runEntity);
                        }

                    }
                    await new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _lPSMonitoringEnroller)
                    .Run(lpsPlan, cancellationToken);
                }
                catch (Exception ex) 
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, ex.Message, LPSLoggingLevel.Error);
                }
            }, LPSCommandLineOptions.LPSRunCommandOptions.TestNameOption);
            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
