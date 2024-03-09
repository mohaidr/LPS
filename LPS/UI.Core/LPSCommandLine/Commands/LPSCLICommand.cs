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
            LPSCommandLineOptions.AddOptionsToCommand(_rootCliCommand, typeof(LPSCommandLineOptions.RootCommandLineOptions));
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _rootCliCommand.SetHandler(async (numberOfRequests, Url) =>
            {
                var plan = new LPSTestPlan(new LPSTestPlan.SetupCommand()
                {
                    Name = "Quick-Test-Plan",
                    NumberOfClients = 1,
                    RunInParallel = true,
                    RampUpPeriod = 1,
                    DelayClientCreationUntilIsNeeded = false,
                }, _logger, _runtimeOperationIdProvider);

                var httpRun = new LPSHttpRun(
                         new LPSHttpRun.SetupCommand()
                         {
                             Mode = LPSHttpRun.IterationMode.R,
                             RequestCount = numberOfRequests,
                             Name = "Quick-Test",
                         }, _logger, _runtimeOperationIdProvider);
                var requestProfile = new LPSHttpRequestProfile(
                    new LPSHttpRequestProfile.SetupCommand()
                    {
                        URL = Url,
                        HttpMethod = "GET",
                        DownloadHtmlEmbeddedResources = false,
                        SaveResponse = false,
                    },
                    _logger, _runtimeOperationIdProvider);
                httpRun.LPSHttpRequestProfile = requestProfile;
                plan.LPSHttpRuns.Add(httpRun);
                var manager = new LPSManager(_logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _lpsMonitoringEnroller);
                await manager.Run(plan, cancellationToken);
            },
            LPSCommandLineOptions.RootCommandLineOptions.NumberOfRequests,
            LPSCommandLineOptions.RootCommandLineOptions.UrlOption);
            _rootCliCommand.Invoke(_args);
        }
    }
}
