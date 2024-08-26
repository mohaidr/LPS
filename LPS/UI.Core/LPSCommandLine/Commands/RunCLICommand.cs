using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
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
    internal class RunCLICommand: ICLICommand
    {
        Command _rootLpsCliCommand;
        TestPlan.SetupCommand _planSetupCommand;
        private string[] _args;
        ILogger _logger;
        IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager;
        IClientConfiguration<HttpRequestProfile> _config;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IWatchdog _watchdog;
        Command _runCommand;
        IMetricsDataMonitor _lPSMonitoringEnroller;
        ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor;
        CancellationTokenSource _cts;
        internal RunCLICommand(Command rootCLICommandLine,
            TestPlan.SetupCommand planSetupCommand,
            ILogger logger,
            IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> httpClientManager,
            IClientConfiguration<HttpRequestProfile> config,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IWatchdog watchdog,
            ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunExecutionCommandStatusMonitor,
            IMetricsDataMonitor lPSMonitoringEnroller,
            CancellationTokenSource cts,
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
            _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
            _lPSMonitoringEnroller = lPSMonitoringEnroller;
            _cts = cts;
            Setup();
        }

        private void Setup()
        {

            _runCommand = new Command("run", "Run existing test");
            CommandLineOptions.AddOptionsToCommand(_runCommand, typeof(CommandLineOptions.LPSRunCommandOptions));
            _rootLpsCliCommand.AddCommand(_runCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _runCommand.SetHandler(async (testName) =>
            {
                try
                {
                    _planSetupCommand = SerializationHelper.Deserialize<TestPlan.SetupCommand>(File.ReadAllText($"{testName}.json"));
                    var lpsPlan = new TestPlan(_planSetupCommand, _logger, _runtimeOperationIdProvider); // it should validate and throw if the command is not valid
                    foreach (var runCommand in _planSetupCommand.LPSRuns)
                    {
                        var runEntity = new HttpRun(runCommand, _logger, _runtimeOperationIdProvider); // must validate and throw if the command is not valid
                        var requestProfile = new HttpRequestProfile(runCommand.LPSRequestProfile, _logger, _runtimeOperationIdProvider);
                        if (runEntity.IsValid && requestProfile.IsValid)
                        {
                            runEntity.SetHttpRequestProfile(requestProfile);
                            lpsPlan.LPSRuns.Add(runEntity);
                        }

                    }
                    await new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider,_httpRunExecutionCommandStatusMonitor, _lPSMonitoringEnroller, _cts).RunAsync(lpsPlan);
                }
                catch (Exception ex) 
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, ex.Message, LPSLoggingLevel.Error);
                }
            }, CommandLineOptions.LPSRunCommandOptions.TestNameOption);
            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
