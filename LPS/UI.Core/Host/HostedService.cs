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
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.UI.Core.Host
{
    internal class HostedService(
        dynamic command_args,
        ILogger logger,
        IClientConfiguration<HttpRequestProfile> config,
        IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> httpClientManager,
        IWatchdog watchdog,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        IMetricsDataMonitor metricDataMonitor,
        ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunExecutionCommandStatusMonitor,
        AppSettingsWritableOptions appSettings, 
        CancellationTokenSource cts) : IHostedService
    {
        readonly ILogger _logger = logger;
        readonly IClientConfiguration<HttpRequestProfile> _config = config;
        readonly IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager = httpClientManager;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;
        readonly IWatchdog _watchdog = watchdog;
        readonly AppSettingsWritableOptions _appSettings = appSettings;
        readonly IMetricsDataMonitor _metricDataMonitor = metricDataMonitor;
        readonly ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
        readonly string[] _command_args = command_args.args;
        readonly CancellationTokenSource _cts = cts;
        static bool _cancelRequested;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has started  --------------", LPSLoggingLevel.Verbose);
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"is the correlation Id of this run", LPSLoggingLevel.Information);

            #pragma warning disable CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
            Console.CancelKeyPress += CancelKeyPressHandler;
            _ = WatchForCancellationAsync();


            TestPlan.SetupCommand lpsTestPlanSetupCommand = new();

            if (_command_args != null && _command_args.Length > 0)
            {
                var commandLineManager = new CommandLineManager(_command_args, _logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _appSettings, lpsTestPlanSetupCommand, _httpRunExecutionCommandStatusMonitor, _metricDataMonitor, _cts);
                commandLineManager.Run(_cts.Token);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Command execution has completed", LPSLoggingLevel.Verbose, cancellationToken);
            }
            else
            {
                var manualBuild = new ManualBuild(new TestPlanValidator(lpsTestPlanSetupCommand), _logger, _runtimeOperationIdProvider);
                var lpsRun = manualBuild.Build(lpsTestPlanSetupCommand);
                File.WriteAllText($"{lpsTestPlanSetupCommand.Name}.json", SerializationHelper.Serialize(lpsTestPlanSetupCommand));


                bool runTest = AnsiConsole.Confirm("Would you like to run your test now?");
                if (runTest)
                {
                    var lpsManager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _httpRunExecutionCommandStatusMonitor, _metricDataMonitor, _cts);
                    await lpsManager.RunAsync(lpsRun);
                }

                AnsiConsole.MarkupLine($"[bold italic]You can use the command [blue]lps run -tn {lpsTestPlanSetupCommand.Name}[/] to execute the plan[/]");
            }
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has completed  --------------", LPSLoggingLevel.Verbose, cancellationToken);
            await _logger.FlushAsync();
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {

            #pragma warning disable CS8602 // Dereference of a possibly null reference.
            await _logger?.LogAsync(_runtimeOperationIdProvider.OperationId, "App Stopping in 5 Seconds", LPSLoggingLevel.Information, cancellationToken);
            await Task.Delay(6000);
            await _logger?.FlushAsync();
            await _logger?.LogAsync(_runtimeOperationIdProvider.OperationId, "--------------  LPS V1 - App Exited  --------------", LPSLoggingLevel.Verbose, cancellationToken);
            _programCompleted = true;
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
        static bool _programCompleted;
        private async Task WatchForCancellationAsync()
        {
            while (!_cancelRequested && !_programCompleted)
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
