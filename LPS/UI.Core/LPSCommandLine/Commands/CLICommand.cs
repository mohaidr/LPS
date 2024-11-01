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
using FluentValidation;

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
            _rootCliCommand.SetHandler(async (planCommand) =>
            {
                ValidationResult planValidationResults, roundValidationResults, iterationValidationResults, requestProfileValidationResults;
                var planSetupCommand = new Plan.SetupCommand { Name = planCommand.Name };
                var roundSetupCommand = planCommand.Rounds[0].Clone();
                var httpIterationSetupCommand = planCommand.Rounds[0].Iterations[0].Clone();
                var lpsRequestProfileSetupCommand = planCommand.Rounds[0].Iterations[0].RequestProfile.Clone();

                var planValidator = new PlanValidator(planSetupCommand);
                planValidationResults = planValidator.Validate();
                var roundValidator = new RoundValidator(roundSetupCommand);
                roundValidationResults = roundValidator.Validate();
                var iterationValidator = new IterationValidator(httpIterationSetupCommand);
                iterationValidationResults = iterationValidator.Validate();
                var lpsRequestProfileValidator = new RequestProfileValidator(lpsRequestProfileSetupCommand);
                requestProfileValidationResults = lpsRequestProfileValidator.Validate();

                if (planValidationResults.IsValid && roundValidationResults.IsValid && iterationValidationResults.IsValid && requestProfileValidationResults.IsValid)
                {
                    var plan = new Plan(planSetupCommand, _logger, _runtimeOperationIdProvider);
                    var testRound = new Round(roundSetupCommand, _logger, _runtimeOperationIdProvider);
                    var httpIteration = new HttpIteration(httpIterationSetupCommand, _logger, _runtimeOperationIdProvider);
                    var requestProfile = new HttpRequestProfile(lpsRequestProfileSetupCommand, _logger, _runtimeOperationIdProvider);
                    httpIteration.SetHttpRequestProfile(requestProfile);
                    testRound.AddIteration(httpIteration);
                    plan.AddRound(testRound);
                    var manager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _httpIterationExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _dashboardConfig, _cts);
                    await manager.RunAsync(plan);
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
