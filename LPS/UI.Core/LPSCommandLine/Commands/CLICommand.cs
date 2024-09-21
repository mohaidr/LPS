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

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class CLICommand : ICLICommand
    {
        private string[] _args;
        ILogger _logger;
        IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager;
        IClientConfiguration<HttpRequestProfile> _config;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IWatchdog _watchdog;
        Command _rootCliCommand;
        IMetricsDataMonitor _lpsMonitoringEnroller;
        ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor;
       CancellationTokenSource _cts;
        public CLICommand(
            Command rootCliCommand,
            ILogger logger,
            IClientManager<HttpRequestProfile, HttpResponse,
            IClientService<HttpRequestProfile, HttpResponse>> httpClientManager,
            IClientConfiguration<HttpRequestProfile> config,
            IWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunExecutionCommandStatusMonitor,
            IMetricsDataMonitor lpsMonitoringEnroller,
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
            _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
            _lpsMonitoringEnroller = lpsMonitoringEnroller;
            _cts = cts;
            Setup();
        }
        private void Setup()
        {
            CommandLineOptions.AddOptionsToCommand(_rootCliCommand, typeof(CommandLineOptions.LPSCommandOptions));
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _rootCliCommand.SetHandler(async (lpsTestPlan) =>
            {
                ValidationResult planValidationResults, runValidationResulta, requestProfileValidationResults;
                var planSetupCommand = new TestPlan.SetupCommand()
                {
                    Name = lpsTestPlan.Name,
                    NumberOfClients = lpsTestPlan.NumberOfClients,
                    RunInParallel = lpsTestPlan.RunInParallel,
                    ArrivalDelay = lpsTestPlan.ArrivalDelay,
                    DelayClientCreationUntilIsNeeded = lpsTestPlan.DelayClientCreationUntilIsNeeded,
                };
                var httpRunSetupCommand = new HttpRun.SetupCommand()
                {
                    Mode = lpsTestPlan.LPSRuns[0].Mode,
                    RequestCount = lpsTestPlan.LPSRuns[0].RequestCount,
                    MaximizeThroughput = lpsTestPlan.LPSRuns[0].MaximizeThroughput,
                    BatchSize = lpsTestPlan.LPSRuns[0].BatchSize,
                    Duration = lpsTestPlan.LPSRuns[0].Duration,
                    CoolDownTime = lpsTestPlan.LPSRuns[0].CoolDownTime,
                    Name = lpsTestPlan.LPSRuns[0].Name,
                };
                var lpsRequestProfileSetupCommand = new HttpRequestProfile.SetupCommand()
                {
                    URL = lpsTestPlan.LPSRuns[0].LPSRequestProfile.URL,
                    HttpMethod = lpsTestPlan.LPSRuns[0].LPSRequestProfile.HttpMethod,
                    DownloadHtmlEmbeddedResources = lpsTestPlan.LPSRuns[0].LPSRequestProfile.DownloadHtmlEmbeddedResources,
                    SaveResponse = lpsTestPlan.LPSRuns[0].LPSRequestProfile.SaveResponse,
                    Payload = lpsTestPlan.LPSRuns[0].LPSRequestProfile.Payload,
                    Httpversion = lpsTestPlan.LPSRuns[0].LPSRequestProfile.Httpversion,
                    HttpHeaders = lpsTestPlan.LPSRuns[0].LPSRequestProfile.HttpHeaders,
                };
                var planValidator = new TestPlanValidator(planSetupCommand);
                planValidationResults = planValidator.Validate();
                var lpsRunValidator = new RunValidator(httpRunSetupCommand);
                runValidationResulta = lpsRunValidator.Validate();
                var lpsRequestProfileValidator = new RequestProfileValidator(lpsRequestProfileSetupCommand);
                requestProfileValidationResults = lpsRequestProfileValidator.Validate();

                if (planValidationResults.IsValid && runValidationResulta.IsValid && requestProfileValidationResults.IsValid)
                {
                    var plan = new TestPlan(planSetupCommand, _logger, _runtimeOperationIdProvider);
                    var httpRun = new HttpRun(httpRunSetupCommand, _logger, _runtimeOperationIdProvider);
                    var requestProfile = new HttpRequestProfile(lpsRequestProfileSetupCommand, _logger, _runtimeOperationIdProvider);
                    httpRun.SetHttpRequestProfile(requestProfile);
                    plan.LPSRuns.Add(httpRun);
                    var manager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _httpRunExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _cts);
                    await manager.RunAsync(plan);
                }
                else
                {
                    planValidationResults.PrintValidationErrors();
                    runValidationResulta.PrintValidationErrors();
                    requestProfileValidationResults.PrintValidationErrors();
                }
            },
            new CommandBinder());
            _rootCliCommand.Invoke(_args);
        }
    }
}
