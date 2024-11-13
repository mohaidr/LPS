using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Monitoring;
using LPS.UI.Common;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Options;
using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using static LPS.UI.Core.LPSCommandLine.CommandLineOptions;
using LPS.UI.Core.Services;
using LPS.DTOs;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class RunCliCommand : ICliCommand
    {
        readonly Command _rootLpsCliCommand;
        private string[] _args;
        readonly ILogger _logger;
        readonly IClientManager<HttpSession, HttpResponse, IClientService<HttpSession, HttpResponse>> _httpClientManager;
        readonly IClientConfiguration<HttpSession> _config;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IWatchdog _watchdog;
        Command _runCommand;
        public Command Command => _runCommand;
        readonly IMetricsDataMonitor _lPSMonitoringEnroller;
        readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandStatusMonitor;
        readonly IOptions<DashboardConfigurationOptions> _dashboardConfig;
        readonly CancellationTokenSource _cts;

        internal RunCliCommand(
            Command rootCLICommandLine,
            ILogger logger,
            IClientManager<HttpSession, HttpResponse, IClientService<HttpSession, HttpResponse>> httpClientManager,
            IClientConfiguration<HttpSession> config,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IWatchdog watchdog,
            ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandStatusMonitor,
            IMetricsDataMonitor lPSMonitoringEnroller,
            IOptions<DashboardConfigurationOptions> dashboardConfig,
            CancellationTokenSource cts,
            string[] args)
        {
            _rootLpsCliCommand = rootCLICommandLine;
            _args = args;
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
            _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
            _lPSMonitoringEnroller = lPSMonitoringEnroller;
            _dashboardConfig = dashboardConfig;
            _cts = cts;
            Setup();
        }

        private void Setup()
        {
            _runCommand = new Command("run", "Run existing test");

            CommandLineOptions.AddOptionsToCommand(_runCommand, typeof(CommandLineOptions.LPSRunCommandOptions));
            // Add the positional argument directly to _runCommand
            _runCommand.AddArgument(LPSRunCommandOptions.ConfigFileArgument);

            _rootLpsCliCommand.AddCommand(_runCommand);
        }

        public void SetHandler(CancellationToken cancellationToken)
        {
            _runCommand.SetHandler(async (string configFile, IList<string> roundNames) =>
            {
                try
                {
                    var planDto = ConfigurationService.FetchConfiguration<PlanDto>(configFile);
                    var plan = new Plan(planDto, _logger, _runtimeOperationIdProvider);
                    if (plan.IsValid)
                    {
                        foreach (var roundDto in planDto.Rounds.Where(round=> roundNames.Count == 0 || roundNames.Contains(round.Name)))
                        {
                            var roundEntity = new Round(roundDto, _logger, _runtimeOperationIdProvider);
                            if (roundEntity.IsValid)
                            {
                                foreach (var iterationDto in roundDto.Iterations)
                                {
                                    var iterationEntity = new HttpIteration(iterationDto, _logger, _runtimeOperationIdProvider);
                                    if (iterationEntity.IsValid)
                                    {
                                        var session = new HttpSession(iterationDto.Session, _logger, _runtimeOperationIdProvider);
                                        if (session.IsValid)
                                        {
                                            iterationEntity.SetHttpSession(session);
                                            roundEntity.AddIteration(iterationEntity);
                                        }
                                    }
                                }

                                foreach (var referencedIteration in roundDto.ReferencedIterations)
                                {
                                    // Find the referenced iteration by name in the global iterations list
                                    var globalIteration = planDto.Iterations.FirstOrDefault(i => i.Name == referencedIteration.Name);
                                    if (globalIteration != null)
                                    {
                                        var referencedIterationEntity = new HttpIteration(globalIteration, _logger, _runtimeOperationIdProvider);
                                        if (referencedIterationEntity.IsValid)
                                        {
                                            var session = new HttpSession(globalIteration.Session, _logger, _runtimeOperationIdProvider);
                                            if (session.IsValid)
                                            {
                                                referencedIterationEntity.SetHttpSession(session);
                                                roundEntity.AddIteration(referencedIterationEntity);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        _logger.Log(_runtimeOperationIdProvider.OperationId, $"Referenced iteration '{referencedIteration.Name}' not found.", LPSLoggingLevel.Warning);
                                    }
                                }
                                plan.AddRound(roundEntity);
                            }
                        }
                        
                    
                    }
                    if (plan.GetReadOnlyRounds().Any())
                    {
                        await new LPSManager(
                            _logger,
                            _httpClientManager,
                            _config,
                            _watchdog,
                            _runtimeOperationIdProvider,
                            _httpIterationExecutionCommandStatusMonitor,
                            _lPSMonitoringEnroller,
                            _dashboardConfig,
                            _cts
                        ).RunAsync(plan);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, $"{ex.Message}\r\n{ex.InnerException?.Message}\r\n{ex.StackTrace}", LPSLoggingLevel.Error);
                }
            }, LPSRunCommandOptions.ConfigFileArgument, 
            LPSRunCommandOptions.RoundNameOption);
        }
    }
}
