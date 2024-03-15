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
        ILPSMonitoringEnroller _lpsMonitoringEnroller;
        public LPSCLICommand(
            Command rootCliCommand,
            ILPSLogger logger,
            ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse,
            ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClientManager,
            ILPSClientConfiguration<LPSHttpRequestProfile> config,
            ILPSWatchdog watchdog,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILPSMonitoringEnroller lpsMonitoringEnroller,
            string[] args)
        {
            _rootCliCommand = rootCliCommand;
            _logger = logger;
            _args = args;
            _config = config;
            _httpClientManager = httpClientManager;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
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
                    Mode = lpsTestPlan.LPSHttpRuns[0].Mode,
                    RequestCount = lpsTestPlan.LPSHttpRuns[0].RequestCount,
                    BatchSize = lpsTestPlan.LPSHttpRuns[0].BatchSize,
                    Duration = lpsTestPlan.LPSHttpRuns[0].Duration,
                    CoolDownTime = lpsTestPlan.LPSHttpRuns[0].CoolDownTime,
                    Name = lpsTestPlan.LPSHttpRuns[0].Name,
                };
                var lpsRequestProfileSetupCommand = new LPSHttpRequestProfile.SetupCommand()
                {
                    URL = lpsTestPlan.LPSHttpRuns[0].LPSRequestProfile.URL,
                    HttpMethod = lpsTestPlan.LPSHttpRuns[0].LPSRequestProfile.HttpMethod,
                    DownloadHtmlEmbeddedResources = lpsTestPlan.LPSHttpRuns[0].LPSRequestProfile.DownloadHtmlEmbeddedResources,
                    SaveResponse = lpsTestPlan.LPSHttpRuns[0].LPSRequestProfile.SaveResponse,
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
                    plan.LPSHttpRuns.Add(httpRun);
                    var manager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _lpsMonitoringEnroller);
                    await manager.Run(plan, cancellationToken);
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
