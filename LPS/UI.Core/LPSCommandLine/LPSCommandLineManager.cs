using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.CommandLine;
using System.IO;
using Newtonsoft.Json;
using System.CommandLine.Binding;
using LPS.Domain.Common.Interfaces;
using LPS.UI.Common;
using System.Threading;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Options;
using LPS.UI.Core.LPSCommandLine.Commands;
using LPS.Domain.Domain.Common.Interfaces;

namespace LPS.UI.Core.LPSCommandLine
{
    public class LPSCommandLineManager
    {
        private string[] _command_args;
        ILPSLogger _logger;
        LPSTestPlan.SetupCommand _command;
        ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> _httpClientManager;
        ILPSClientConfiguration<LPSHttpRequestProfile> _config;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSWatchdog _watchdog;
        Command _rootCliCommand;
        LPSCLICommand _lpsCliCommand;
        LPSCreateCLICommand _lpsCreateCliCommand;
        LPSAddCLICommand _lpsAddCliCommand;
        LPSRunCLICommand _lpsRunCliCommand;
        LPSLoggerCLICommand _lpsLoggerCliCommand;
        LPSWatchDogCLICommand _lpsSWatchdogCliCommand;
        LPSHttpClientCLICommand _lpsSHttpClientCliCommand;
        LPSAppSettingsWritableOptions _appSettings;
        ILPSMetricsDataMonitor _lpsMonitoringEnroller;
        CancellationTokenSource _cts;
        ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> _httpRunExecutionCommandStatusMonitor;
        public LPSCommandLineManager(
            string[] command_args,
            ILPSLogger logger,
            ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse,
            ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClientManager,
            ILPSClientConfiguration<LPSHttpRequestProfile> config,
            ILPSWatchdog watchdog,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
            LPSAppSettingsWritableOptions appSettings,
            LPSTestPlan.SetupCommand command,
            ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> httpRunExecutionCommandStatusMonitor,
            ILPSMetricsDataMonitor lpsMonitoringEnroller,
            CancellationTokenSource cts)
        {
            _logger = logger;
            _command = command;
            _command_args = command_args;
            _config = config;
            _httpClientManager = httpClientManager;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _appSettings = appSettings;
            _lpsMonitoringEnroller = lpsMonitoringEnroller;
            _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
            _cts = cts;
            Configure();
        }
        public LPSTestPlan.SetupCommand Command { get { return _command; } }

        private void Configure()
        {
            _rootCliCommand = new Command("lps", "Load, Performance and Stress Testing Command Tool.");
            _lpsCliCommand = new LPSCLICommand(_rootCliCommand, _logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _httpRunExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _cts, _command_args);
            _lpsCreateCliCommand = new LPSCreateCLICommand(_rootCliCommand, _command, _command_args);
            _lpsAddCliCommand = new LPSAddCLICommand(_rootCliCommand, _command, _command_args);
            _lpsRunCliCommand = new LPSRunCLICommand(_rootCliCommand, _command, _logger, _httpClientManager, _config, _runtimeOperationIdProvider, _watchdog,_httpRunExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _cts, _command_args);
            _lpsLoggerCliCommand = new LPSLoggerCLICommand(_rootCliCommand, _logger, _runtimeOperationIdProvider, _appSettings.LPSFileLoggerOptions, _command_args);
            _lpsSHttpClientCliCommand = new LPSHttpClientCLICommand(_rootCliCommand, _logger, _runtimeOperationIdProvider, _appSettings.LPSHttpClientOptions, _command_args);
            _lpsSWatchdogCliCommand = new LPSWatchDogCLICommand(_rootCliCommand, _logger, _runtimeOperationIdProvider, _appSettings.LPSWatchdogOptions, _command_args);
        }

        public void Run(CancellationToken cancellationToken)
        {
            string joinedCommand = string.Join(" ", _command_args);

            if (joinedCommand.StartsWith("create", StringComparison.OrdinalIgnoreCase))
            {
                _lpsCreateCliCommand.Execute(cancellationToken);
            }
            else if (joinedCommand.StartsWith("add", StringComparison.OrdinalIgnoreCase))
            {
                _lpsAddCliCommand.Execute(cancellationToken);
            }
            else if (joinedCommand.StartsWith("run", StringComparison.OrdinalIgnoreCase))
            {
                _lpsRunCliCommand.Execute(cancellationToken);
            }
            else if (joinedCommand.StartsWith("logger", StringComparison.OrdinalIgnoreCase))

            {
                _lpsLoggerCliCommand.Execute(cancellationToken);
            }
            else if (joinedCommand.StartsWith("httpclient", StringComparison.OrdinalIgnoreCase))

            {
                _lpsSHttpClientCliCommand.Execute(cancellationToken);
            }
            else if (joinedCommand.StartsWith("watchdog", StringComparison.OrdinalIgnoreCase))

            {
                _lpsSWatchdogCliCommand.Execute(cancellationToken);
            }
            else
            {
                _lpsCliCommand.Execute(cancellationToken);
            }
        }
    }
}
