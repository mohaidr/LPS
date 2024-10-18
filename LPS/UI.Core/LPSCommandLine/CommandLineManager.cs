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
    public class CommandLineManager
    {
        private string[] _command_args;
        ILogger _logger;
        TestPlan.SetupCommand _command;
        IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager;
        IClientConfiguration<HttpRequestProfile> _config;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IWatchdog _watchdog;
        Command _rootCliCommand;
        CLICommand _lpsCliCommand;
        CreateCLICommand _lpsCreateCliCommand;
        AddCLICommand _lpsAddCliCommand;
        RunCLICommand _lpsRunCliCommand;
        LoggerCLICommand _lpsLoggerCliCommand;
        WatchDogCLICommand _lpsSWatchdogCliCommand;
        HttpClientCLICommand _lpsSHttpClientCliCommand;
        AppSettingsWritableOptions _appSettings;
        IMetricsDataMonitor _lpsMonitoringEnroller;
        CancellationTokenSource _cts;
        ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor;
        
        #pragma warning disable CS8618
        public CommandLineManager(
            string[] command_args,
            ILogger logger,
            IClientManager<HttpRequestProfile, HttpResponse,
            IClientService<HttpRequestProfile, HttpResponse>> httpClientManager,
            IClientConfiguration<HttpRequestProfile> config,
            IWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            AppSettingsWritableOptions appSettings,
            TestPlan.SetupCommand command,
            ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunExecutionCommandStatusMonitor,
            IMetricsDataMonitor lpsMonitoringEnroller,
            CancellationTokenSource cts)
        {
            _logger = logger;
            _command = command;
            _command_args = command_args;
            _command_args = command_args.Select(arg => arg.ToLowerInvariant()).ToArray();
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
        public TestPlan.SetupCommand Command { get { return _command; } }

        private void Configure()
        {
            _rootCliCommand = new Command("lps", "Load, Performance and Stress Testing Command Tool.");
            _lpsCliCommand = new CLICommand(_rootCliCommand, _logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _httpRunExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _cts, _command_args);
            _lpsCreateCliCommand = new CreateCLICommand(_rootCliCommand, _command, _command_args);
            _lpsAddCliCommand = new AddCLICommand(_rootCliCommand, _command, _command_args);
            _lpsRunCliCommand = new RunCLICommand(_rootCliCommand, _command, _logger, _httpClientManager, _config, _runtimeOperationIdProvider, _watchdog,_httpRunExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _cts, _command_args);
            _lpsLoggerCliCommand = new LoggerCLICommand(_rootCliCommand, _logger, _runtimeOperationIdProvider, _appSettings.LPSFileLoggerOptions, _command_args);
            _lpsSHttpClientCliCommand = new HttpClientCLICommand(_rootCliCommand, _logger, _runtimeOperationIdProvider, _appSettings.LPSHttpClientOptions, _command_args);
            _lpsSWatchdogCliCommand = new WatchDogCLICommand(_rootCliCommand, _logger, _runtimeOperationIdProvider, _appSettings.LPSWatchdogOptions, _command_args);
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
