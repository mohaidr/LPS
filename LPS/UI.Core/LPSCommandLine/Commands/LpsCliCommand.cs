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
using LPS.UI.Core.Services;
using LPS.DTOs;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class LpsCliCommand : ICliCommand
    {
        private readonly string[] _args;
        readonly ILogger _logger;
        readonly IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager;
        readonly IClientConfiguration<HttpRequestProfile> _config;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IWatchdog _watchdog;
        readonly Command _rootCliCommand;
        public Command Command => _rootCliCommand;
        readonly IMetricsDataMonitor _lpsMonitoringEnroller;
        readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandStatusMonitor;
        readonly CancellationTokenSource _cts;
        readonly IOptions<DashboardConfigurationOptions> _dashboardConfig;
        public LpsCliCommand(
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

        public void SetHandler(CancellationToken cancellationToken)
        {
            _rootCliCommand.SetHandler(async (planDto, save) =>
            {
                ValidationResult planValidationResults, roundValidationResults, iterationValidationResults, requestProfileValidationResults;
                planDto.DeepCopy(out PlanDto planDtoCopy);
                var roundDto = planDto.Rounds[0];
                var iterationDto = planDto.Rounds[0].Iterations[0];
                var planValidator = new PlanValidator(planDtoCopy);
                planValidationResults = planValidator.Validate();
                var roundValidator = new RoundValidator(planDto.Rounds[0]);
                roundValidationResults = roundValidator.Validate();
                var iterationValidator = new IterationValidator(planDto.Rounds[0].Iterations[0]);
                iterationValidationResults = iterationValidator.Validate();
                var lpsRequestProfileValidator = new RequestProfileValidator(iterationDto.RequestProfile);
                requestProfileValidationResults = lpsRequestProfileValidator.Validate();

                if (planValidationResults.IsValid && roundValidationResults.IsValid && iterationValidationResults.IsValid && requestProfileValidationResults.IsValid)
                {
                    var plan = new Plan(planDtoCopy, _logger, _runtimeOperationIdProvider);
                    var testRound = new Round(roundDto, _logger, _runtimeOperationIdProvider);
                    var httpIteration = new HttpIteration(iterationDto, _logger, _runtimeOperationIdProvider);
                    var requestProfile = new HttpRequestProfile(iterationDto.RequestProfile, _logger, _runtimeOperationIdProvider);
                    httpIteration.SetHttpRequestProfile(requestProfile);
                    testRound.AddIteration(httpIteration);
                    plan.AddRound(testRound);
                    var manager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _httpIterationExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _dashboardConfig, _cts);
                    if (save)
                    {
                        ConfigurationService.SaveConfiguration($"{planDtoCopy.Name}.yaml", planDtoCopy);
                        ConfigurationService.SaveConfiguration($"{planDtoCopy.Name}.json", planDtoCopy);
                    }
                    await manager.RunAsync(plan);
                }
                else
                {
                    roundValidationResults.PrintValidationErrors();
                    iterationValidationResults.PrintValidationErrors();
                    requestProfileValidationResults.PrintValidationErrors();
                }
            },
            new CommandBinder(),
            CommandLineOptions.LPSCommandOptions.SaveOption);
        }
    }
}
