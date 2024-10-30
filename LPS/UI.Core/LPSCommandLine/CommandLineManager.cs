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
        IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager;
        IClientConfiguration<HttpRequestProfile> _config;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IWatchdog _watchdog;
        Command _rootCliCommand;
        CLICommand _lpsCliCommand;
        CreateCLICommand _lpsCreateCliCommand;
        RoundCLICommand _lpsRoundCliCommand;
        IterationCLICommand _lpsIterationCliCommand;
        RunCLICommand _lpsRunCliCommand;
        LoggerCLICommand _lpsLoggerCliCommand;
        WatchDogCLICommand _lpsSWatchdogCliCommand;
        HttpClientCLICommand _lpsSHttpClientCliCommand;
        readonly AppSettingsWritableOptions _appSettings;
        readonly IMetricsDataMonitor _lpsMonitoringEnroller;
        readonly CancellationTokenSource _cts;
        readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandStatusMonitor;

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
            ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandStatusMonitor,
            IMetricsDataMonitor lpsMonitoringEnroller,
            CancellationTokenSource cts)
        {
            _logger = logger;
            _command_args = command_args;
            _command_args = command_args.Select(arg => arg.ToLowerInvariant()).ToArray();
            _config = config;
            _httpClientManager = httpClientManager;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _appSettings = appSettings;
            _lpsMonitoringEnroller = lpsMonitoringEnroller;
            _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
            _cts = cts;
            Configure();
        }
        private void Configure()
        {
            _rootCliCommand = new Command("lps", "Load, Performance and Stress Testing Command Tool.");
            _lpsCliCommand = new CLICommand(_rootCliCommand, _logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _httpIterationExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _appSettings.DashboardConfigurationOptions, _cts, _command_args);
            _lpsCreateCliCommand = new CreateCLICommand(_rootCliCommand, _logger, _httpClientManager, _config, _runtimeOperationIdProvider, _watchdog, _httpIterationExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _appSettings.DashboardConfigurationOptions, _cts, _command_args);
            _lpsRoundCliCommand = new RoundCLICommand(_rootCliCommand, _command_args);
            _lpsIterationCliCommand = new IterationCLICommand(_rootCliCommand, _command_args);
            _lpsRunCliCommand = new RunCLICommand(_rootCliCommand, _logger, _httpClientManager, _config, _runtimeOperationIdProvider, _watchdog,_httpIterationExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _appSettings.DashboardConfigurationOptions, _cts, _command_args);
            _lpsLoggerCliCommand = new LoggerCLICommand(_rootCliCommand, _logger, _runtimeOperationIdProvider, _appSettings.LPSFileLoggerOptions, _command_args);
            _lpsSHttpClientCliCommand = new HttpClientCLICommand(_rootCliCommand, _logger, _runtimeOperationIdProvider, _appSettings.LPSHttpClientOptions, _command_args);
            _lpsSWatchdogCliCommand = new WatchDogCLICommand(_rootCliCommand, _logger, _runtimeOperationIdProvider, _appSettings.LPSWatchdogOptions, _command_args);
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            string joinedCommand = string.Join(" ", _command_args);

            switch (joinedCommand.ToLowerInvariant())
            {
                case string cmd when cmd.StartsWith("create"):
                    Console.WriteLine(joinedCommand);
                    await _lpsCreateCliCommand.ExecuteAsync(cancellationToken);
                    break;

                case string cmd when cmd.StartsWith("round"):
                    await _lpsRoundCliCommand.ExecuteAsync(cancellationToken);
                    break;

                case string cmd when cmd.StartsWith("iteration"):
                    await _lpsIterationCliCommand.ExecuteAsync(cancellationToken);
                    break;

                case string cmd when cmd.StartsWith("run"):
                    await _lpsRunCliCommand.ExecuteAsync(cancellationToken);
                    break;

                case string cmd when cmd.StartsWith("logger"):
                    await _lpsLoggerCliCommand.ExecuteAsync(cancellationToken);
                    break;

                case string cmd when cmd.StartsWith("httpclient"):
                    await _lpsSHttpClientCliCommand.ExecuteAsync(cancellationToken);
                    break;

                case string cmd when cmd.StartsWith("watchdog"):
                    await _lpsSWatchdogCliCommand.ExecuteAsync(cancellationToken);
                    break;

                default:
                    await _lpsCliCommand.ExecuteAsync(cancellationToken);
                    break;
            }
        }
    }
}
