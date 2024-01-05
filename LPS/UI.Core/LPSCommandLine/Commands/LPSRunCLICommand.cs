using LPS.Domain;
using LPS.Domain.Common;
using LPS.UI.Common;
using LPS.UI.Common.Helpers;
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
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSWatchdog _watchdog;
        Command _runCommand;
        internal LPSRunCLICommand(Command rootCLICommandLine,
            LPSTestPlan.SetupCommand planSetupCommand,
            ILPSLogger logger,
            ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClientManager,
            ILPSClientConfiguration<LPSHttpRequestProfile> config,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILPSWatchdog watchdog,
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
                    await new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider)
                    .Run(_planSetupCommand, cancellationToken);
                }
                catch (Exception ex) {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, ex.Message, LPSLoggingLevel.Error);
                }
            }, LPSCommandLineOptions.LPSRunCommandOptions.TestNameOption);
            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
