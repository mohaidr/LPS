using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.UI.Common;
using LPS.UI.Common.Options;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.UI.Core.LPSCommandLine.Bindings;
using FluentValidation.Results;
using LPS.UI.Common.Extensions;
using LPS.UI.Core.LPSValidators;
using HtmlAgilityPack;
using LPS.Domain.Domain.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class CLICommand : ICLICommand
    {
        private readonly string[] _args;
        readonly ILogger _logger;
        readonly IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager;
        readonly IClientConfiguration<HttpRequestProfile> _config;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IWatchdog _watchdog;
        readonly Command _rootCliCommand;
        readonly IMetricsDataMonitor _lpsMonitoringEnroller;
        readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandStatusMonitor;
        readonly CancellationTokenSource _cts;
        readonly IOptions<DashboardConfigurationOptions> _dashboardConfig;
        public CLICommand(
            Command rootCliCommand,
            ILogger logger,
            IClientManager<HttpRequestProfile, HttpResponse,
            IClientService<HttpRequestProfile, HttpResponse>> httpClientManager,
            IClientConfiguration<HttpRequestProfile> config,
            IWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandStatusMonitor,
            IMetricsDataMonitor lpsMonitoringEnroller,
            IOptions<DashboardConfigurationOptions> dashboardConfig,
            CancellationTokenSource cts,
            string[] args)
        {
            _rootCliCommand = rootCliCommand;
            _logger = logger;
            _args = args;
            _config = config;
            _httpClientManager = httpClientManager;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
            _lpsMonitoringEnroller = lpsMonitoringEnroller;
            _dashboardConfig = dashboardConfig;
            _cts = cts;
            Setup();
        }
        private void Setup()
        {
            CommandLineOptions.AddOptionsToCommand(_rootCliCommand, typeof(CommandLineOptions.LPSCommandOptions));
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _rootCliCommand.SetHandler(async (round) =>
            {
                ValidationResult roundValidationResults, iterationValidationResults, requestProfileValidationResults;
                var roundSetupCommand = new Round.SetupCommand()
                {
                    Name = round.Name,
                    NumberOfClients = round.NumberOfClients,
                    RunInParallel = round.RunInParallel,
                    ArrivalDelay = round.ArrivalDelay,
                    DelayClientCreationUntilIsNeeded = round.DelayClientCreationUntilIsNeeded,
                };
                var httpIterationSetupCommand = new HttpIteration.SetupCommand()
                {
                    Mode = round.Iterations[0].Mode,
                    RequestCount = round.Iterations[0].RequestCount,
                    MaximizeThroughput = round.Iterations[0].MaximizeThroughput,
                    BatchSize = round.Iterations[0].BatchSize,
                    Duration = round.Iterations[0].Duration,
                    CoolDownTime = round.Iterations[0].CoolDownTime,
                    Name = round.Iterations[0].Name,
                };
                var lpsRequestProfileSetupCommand = new HttpRequestProfile.SetupCommand()
                {
                    URL = round.Iterations[0].RequestProfile.URL,
                    HttpMethod = round.Iterations[0].RequestProfile.HttpMethod,
                    DownloadHtmlEmbeddedResources = round.Iterations[0].RequestProfile.DownloadHtmlEmbeddedResources,
                    SaveResponse = round.Iterations[0].RequestProfile.SaveResponse,
                    SupportH2C = round.Iterations[0].RequestProfile.SupportH2C,
                    Payload = round.Iterations[0].RequestProfile.Payload,
                    HttpVersion = round.Iterations[0].RequestProfile.HttpVersion,
                    HttpHeaders = round.Iterations[0].RequestProfile.HttpHeaders,
                };
                var roundValidator = new RoundValidator(roundSetupCommand);
                roundValidationResults = roundValidator.Validate();
                var iterationValidator = new IterationValidator(httpIterationSetupCommand);
                iterationValidationResults = iterationValidator.Validate();
                var lpsRequestProfileValidator = new RequestProfileValidator(lpsRequestProfileSetupCommand);
                requestProfileValidationResults = lpsRequestProfileValidator.Validate();

                if (roundValidationResults.IsValid && iterationValidationResults.IsValid && requestProfileValidationResults.IsValid)
                {
                    var testRound = new Round(roundSetupCommand, _logger, _runtimeOperationIdProvider);
                    var httpIteration = new HttpIteration(httpIterationSetupCommand, _logger, _runtimeOperationIdProvider);
                    var requestProfile = new HttpRequestProfile(lpsRequestProfileSetupCommand, _logger, _runtimeOperationIdProvider);
                    httpIteration.SetHttpRequestProfile(requestProfile);
                    testRound.AddIteration(httpIteration);
                    var manager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _httpIterationExecutionCommandStatusMonitor, _lpsMonitoringEnroller,_dashboardConfig, _cts);
                   // await manager.RunAsync(testRound);
                }
                else
                {
                    roundValidationResults.PrintValidationErrors();
                    iterationValidationResults.PrintValidationErrors();
                    requestProfileValidationResults.PrintValidationErrors();
                }
            },
            new CommandBinder());
            await _rootCliCommand.InvokeAsync(_args);
        }
    }
}
