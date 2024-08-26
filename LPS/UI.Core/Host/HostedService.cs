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
using LPS.Domain.Domain.Common.Interfaces;

namespace LPS.UI.Core.Host
{
    internal class HostedService : IHostedService
    {
        ILogger _logger;
        IClientConfiguration<HttpRequestProfile> _config;
        IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IWatchdog _watchdog;
        AppSettingsWritableOptions _appSettings;
        IMetricsDataMonitor _lPSMonitoringEnroller;
        ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor;
        string[] _command_args;
        CancellationTokenSource _cts;
        static bool _cancelRequested;
        public HostedService(
            dynamic command_args,
            ILogger logger,
            IClientConfiguration<HttpRequestProfile> config,
            IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> httpClientManager,
            IWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsDataMonitor lPSMonitoringEnroller,
            ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunExecutionCommandStatusMonitor,
            AppSettingsWritableOptions appSettings, CancellationTokenSource cts)
        {
            _logger = logger;
            _config = config;
            _command_args = command_args.args;
            _httpClientManager = httpClientManager;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _appSettings = appSettings;
            _lPSMonitoringEnroller = lPSMonitoringEnroller;
            _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
            _cts = cts;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has started  --------------", LPSLoggingLevel.Verbose);
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"is the correlation Id of this run", LPSLoggingLevel.Information);

            Console.CancelKeyPress += CancelKeyPressHandler;
            _ = WatchForCancellationAsync();


            TestPlan.SetupCommand lpsTestPlanSetupCommand = new TestPlan.SetupCommand();

            if (_command_args != null && _command_args.Length > 0)
            {
                var commandLineManager = new CommandLineManager(_command_args, _logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _appSettings, lpsTestPlanSetupCommand, _httpRunExecutionCommandStatusMonitor, _lPSMonitoringEnroller, _cts);
                commandLineManager.Run(_cts.Token);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Command execution has completed", LPSLoggingLevel.Verbose);
            }
            else
            {
                var manualBuild = new ManualBuild(new TestPlanValidator(lpsTestPlanSetupCommand), _logger, _runtimeOperationIdProvider);
                var lpsRun = manualBuild.Build(lpsTestPlanSetupCommand);
                File.WriteAllText($"{lpsTestPlanSetupCommand.Name}.json", SerializationHelper.Serialize(lpsTestPlanSetupCommand));


                bool runTest = AnsiConsole.Confirm("Would you like to run your test now?");
                if (runTest)
                {
                    var lpsManager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _httpRunExecutionCommandStatusMonitor, _lPSMonitoringEnroller, _cts);
                    await lpsManager.RunAsync(lpsRun);
                }

                AnsiConsole.MarkupLine($"[bold italic]You can use the command [blue]lps run -tn {lpsTestPlanSetupCommand.Name}[/] to execute the plan[/]");
            }
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has completed  --------------", LPSLoggingLevel.Verbose);

            await _logger.Flush();
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {

            #pragma warning disable CS8602 // Dereference of a possibly null reference.
            await _logger?.LogAsync(_runtimeOperationIdProvider.OperationId, "App Stopping in 5 Seconds", LPSLoggingLevel.Information);
            await Task.Delay(6000);
            await _logger?.LogAsync(_runtimeOperationIdProvider.OperationId, "--------------  LPS V1 - App Exited  --------------", LPSLoggingLevel.Verbose);
            await _logger?.Flush();
        }

        private void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            if (e.SpecialKey == ConsoleSpecialKey.ControlC || e.SpecialKey == ConsoleSpecialKey.ControlBreak)
            {
                e.Cancel = true; // Prevent default process termination.
                AnsiConsole.MarkupLine("[yellow]Graceful shutdown requested (Ctrl+C/Break).[/]");
                RequestCancellation(); // Cancel the CancellationTokenSource.
            }
        }

        private async Task WatchForCancellationAsync()
        {
            while (!_cancelRequested)
            {
                if (Console.KeyAvailable) // Check for the Escape key
                {
                    var keyInfo = Console.ReadKey(true);
                    if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        AnsiConsole.MarkupLine("[yellow]Graceful shutdown requested (Escape).[/]");
                        RequestCancellation(); // Cancel the CancellationTokenSource.
                        break; // Exit the loop
                    }
                }
                await Task.Delay(1000); // Poll every second
            }
        }

        private void RequestCancellation()
        {
            if (!_cancelRequested)
            {
                _cancelRequested = true;
                AnsiConsole.MarkupLine("[yellow]Gracefully shutting down the LPS local server[/]");
                _cts.Cancel();
            }
        }

    }
}
