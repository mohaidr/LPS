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
    internal class LPSCLICommand : ILPSCLICommand
    {
        private string[] _args;
        ILPSLogger _logger;
        ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> _httpClientManager;
        ILPSClientConfiguration<LPSHttpRequestProfile> _config;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSWatchdog _watchdog;
        Command _rootCliCommand;
        ILPSMetricsDataMonitor _lpsMonitoringEnroller;
        ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> _httpRunExecutionCommandStatusMonitor;
        public LPSCLICommand(
            Command rootCliCommand,
            ILPSLogger logger,
            ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse,
            ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClientManager,
            ILPSClientConfiguration<LPSHttpRequestProfile> config,
            ILPSWatchdog watchdog,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
            ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> httpRunExecutionCommandStatusMonitor,
            ILPSMetricsDataMonitor lpsMonitoringEnroller,
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
            Setup();
        }
        private void Setup()
        {
            LPSCommandLineOptions.AddOptionsToCommand(_rootCliCommand, typeof(LPSCommandLineOptions.LPSCommandOptions));
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _rootCliCommand.SetHandler(async (lpsTestPlan) =>
            {
                ValidationResult planValidationResults, runValidationResulta, requestProfileValidationResults;
                var planSetupCommand = new LPSTestPlan.SetupCommand()
                {
                    Name = lpsTestPlan.Name,
                    NumberOfClients = lpsTestPlan.NumberOfClients,
                    RunInParallel = lpsTestPlan.RunInParallel,
                    RampUpPeriod = lpsTestPlan.RampUpPeriod,
                    DelayClientCreationUntilIsNeeded = lpsTestPlan.DelayClientCreationUntilIsNeeded,
                };
                var httpRunSetupCommand = new LPSHttpRun.SetupCommand()
                {
                    Mode = lpsTestPlan.LPSRuns[0].Mode,
                    RequestCount = lpsTestPlan.LPSRuns[0].RequestCount,
                    BatchSize = lpsTestPlan.LPSRuns[0].BatchSize,
                    Duration = lpsTestPlan.LPSRuns[0].Duration,
                    CoolDownTime = lpsTestPlan.LPSRuns[0].CoolDownTime,
                    Name = lpsTestPlan.LPSRuns[0].Name,
                };
                var lpsRequestProfileSetupCommand = new LPSHttpRequestProfile.SetupCommand()
                {
                    URL = lpsTestPlan.LPSRuns[0].LPSRequestProfile.URL,
                    HttpMethod = lpsTestPlan.LPSRuns[0].LPSRequestProfile.HttpMethod,
                    DownloadHtmlEmbeddedResources = lpsTestPlan.LPSRuns[0].LPSRequestProfile.DownloadHtmlEmbeddedResources,
                    SaveResponse = lpsTestPlan.LPSRuns[0].LPSRequestProfile.SaveResponse,
                    Payload = lpsTestPlan.LPSRuns[0].LPSRequestProfile.Payload,
                    Httpversion = lpsTestPlan.LPSRuns[0].LPSRequestProfile.Httpversion,
                    HttpHeaders = lpsTestPlan.LPSRuns[0].LPSRequestProfile.HttpHeaders,
                };
                var planValidator = new LPSTestPlanValidator(planSetupCommand);
                planValidationResults = planValidator.Validate();
                var lpsRunValidator = new LPSRunValidator(httpRunSetupCommand);
                runValidationResulta = lpsRunValidator.Validate();
                var lpsRequestProfileValidator = new LPSRequestProfileValidator(lpsRequestProfileSetupCommand);
                requestProfileValidationResults = lpsRequestProfileValidator.Validate();

                if (planValidationResults.IsValid && runValidationResulta.IsValid && requestProfileValidationResults.IsValid)
                {
                    var plan = new LPSTestPlan(planSetupCommand, _logger, _runtimeOperationIdProvider);
                    var httpRun = new LPSHttpRun(httpRunSetupCommand, _logger, _runtimeOperationIdProvider);
                    var requestProfile = new LPSHttpRequestProfile(lpsRequestProfileSetupCommand, _logger, _runtimeOperationIdProvider);
                    httpRun.LPSHttpRequestProfile = requestProfile;
                    plan.LPSRuns.Add(httpRun);
                    var manager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _httpRunExecutionCommandStatusMonitor, _lpsMonitoringEnroller);
                    await manager.RunAsync(plan, cancellationToken);
                }
                else
                {
                    planValidationResults.PrintValidationErrors();
                    runValidationResulta.PrintValidationErrors();
                    requestProfileValidationResults.PrintValidationErrors();
                }
            },
            new LPSCommandBinder());
            _rootCliCommand.Invoke(_args);
        }
    }
}
