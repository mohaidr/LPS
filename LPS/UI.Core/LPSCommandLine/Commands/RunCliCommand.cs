using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.DTOs;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.LPSClients.GlobalVariableManager;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.Monitoring;
using LPS.UI.Common;
using LPS.UI.Core.Services;
using LPS.UI.Core.LPSValidators;
using Microsoft.Extensions.Options;
using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using AutoMapper;
using LPS.Domain.Common;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSFlow.LPSHandlers;
using LPS.UI.Common.Options;
using static LPS.UI.Core.LPSCommandLine.CommandLineOptions;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class RunCliCommand : ICliCommand
    {
        private readonly Command _rootLpsCliCommand;
        private readonly ILogger _logger;
        private readonly IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _httpClientManager;
        private readonly IClientConfiguration<HttpRequest> _config;
        private readonly IVariableManager _variableManager;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly IWatchdog _watchdog;
        private readonly IPlaceholderResolverService _placeholderResolverService;
        private readonly IMetricsDataMonitor _lpsMonitoringEnroller;
        private readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandStatusMonitor;
        private readonly IOptions<DashboardConfigurationOptions> _dashboardConfig;
        private readonly CancellationTokenSource _cts;
        private readonly IMapper _mapper; // AutoMapper instance
        private Command _runCommand;

        public Command Command => _runCommand;

        internal RunCliCommand(
            Command rootCLICommandLine,
            ILogger logger,
            IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> httpClientManager,
            IClientConfiguration<HttpRequest> config,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IWatchdog watchdog,
            ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandStatusMonitor,
            IMetricsDataMonitor lpsMonitoringEnroller,
            IOptions<DashboardConfigurationOptions> dashboardConfig,
            IMapper mapper,
            IVariableManager variableManager,
            IPlaceholderResolverService placeholderResolverService,
            CancellationTokenSource cts) // Injected AutoMapper
        {
            _rootLpsCliCommand = rootCLICommandLine;
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
            _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
            _lpsMonitoringEnroller = lpsMonitoringEnroller;
            _dashboardConfig = dashboardConfig;
            _cts = cts;
            _variableManager = variableManager;
            _placeholderResolverService = placeholderResolverService;
            _mapper = mapper; // Assign mapper
            Setup();
        }

        private void Setup()
        {
            _runCommand = new Command("run", "Run existing test");
            CommandLineOptions.AddOptionsToCommand(_runCommand, typeof(CommandLineOptions.LPSRunCommandOptions));
            _runCommand.AddArgument(LPSRunCommandOptions.ConfigFileArgument);
            _rootLpsCliCommand.AddCommand(_runCommand);
        }

        public void SetHandler(CancellationToken cancellationToken)
        {
            _runCommand.SetHandler(async (string configFile, IList<string> roundNames, IList<string> tags, IList<string> environments) =>
            {
                try
                {
                    var planDto = ConfigurationService.FetchConfiguration<PlanDto>(configFile, _placeholderResolverService);
                    var planCommand = _mapper.Map<Plan.SetupCommand>(planDto);
                    var plan = new Plan(planCommand, _logger, _runtimeOperationIdProvider, _placeholderResolverService);
                    if (plan.IsValid)
                    {
                        var variableValidator = new VariableValidator();
                        var environmentValidator = new EnvironmentValidator();

                        // Global Variables
                        foreach (var variableDto in planDto.Variables)
                        {
                            variableValidator.ValidateAndThrow(variableDto);
                            var variableHolder = await BuildVariableHolder(variableDto, true, cancellationToken);
                            _variableManager.AddVariableAsync(variableDto.Name, variableHolder, cancellationToken).Wait();
                        }

                        // Environment-Specific Variables
                        foreach (var environmentName in environments)
                        {
                            var environmentDto = planDto.Environments
                                .FirstOrDefault(env => env.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

                            if (environmentDto != null)
                            {
                                environmentValidator.ValidateAndThrow(environmentDto);

                                foreach (var variableDto in environmentDto.Variables)
                                {
                                    variableValidator.ValidateAndThrow(variableDto);
                                    var variableHolder = await BuildVariableHolder(variableDto, false, cancellationToken);
                                    _variableManager.AddVariableAsync(variableDto.Name, variableHolder, cancellationToken).Wait();
                                }
                            }
                            else
                            {
                                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Environment '{environmentName}' not found.", LPSLoggingLevel.Warning);
                            }
                        }

                        // Rounds and Iterations
                        foreach (var roundDto in planDto.Rounds.Where(round =>
                            roundNames.Count == 0 && tags.Count == 0 ||
                            roundNames.Contains(round.Name, StringComparer.OrdinalIgnoreCase) ||
                            round.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase))))
                        {
                            var roundCommand = _mapper.Map<Round.SetupCommand>(roundDto);
                            var roundEntity = new Round(roundCommand, _logger, _runtimeOperationIdProvider);

                            if (roundEntity.IsValid)
                            {
                                foreach (var iterationDto in roundDto.Iterations)
                                {
                                    if (iterationDto.HttpRequest?.URL != null && roundDto?.BaseUrl != null && !iterationDto.HttpRequest.URL.StartsWith("http://") && !iterationDto.HttpRequest.URL.StartsWith("https://"))
                                    {
                                        if (iterationDto.HttpRequest.URL.StartsWith("$") && roundDto.BaseUrl.StartsWith("$"))
                                        {
                                            throw new InvalidOperationException("Either the base URL or the local URL is defined as a variable, but runtime handling of both as variables is not supported. Consider setting the base URL as a global variable and reusing it in the local variable.");
                                        }
                                        iterationDto.HttpRequest.URL = $"{roundDto.BaseUrl}{iterationDto.HttpRequest.URL}";
                                    }
                                    var iterationCommand = _mapper.Map<HttpIteration.SetupCommand>(iterationDto);

                                    var iterationEntity = new HttpIteration(iterationCommand, _logger, _runtimeOperationIdProvider);

                                    if (iterationEntity.IsValid)
                                    {
                                        var requestCommand = _mapper.Map<HttpRequest.SetupCommand>(iterationDto.HttpRequest);
                                        var requestEntity = new HttpRequest(requestCommand, _logger, _runtimeOperationIdProvider, _placeholderResolverService);

                                        if (requestEntity.IsValid)
                                        {
                                            if (iterationDto.HttpRequest.Capture != null)
                                            {
                                                var captureCommand = _mapper.Map<CaptureHandler.SetupCommand>(iterationDto.HttpRequest.Capture);
                                                var captureEntity = new CaptureHandler(captureCommand, _logger, _runtimeOperationIdProvider);
                                                if (captureEntity.IsValid)
                                                    requestEntity.SetCapture(captureEntity);
                                            }

                                            iterationEntity.SetHttpRequest(requestEntity);
                                            roundEntity.AddIteration(iterationEntity);
                                        }
                                    }
                                }
                                plan.AddRound(roundEntity);
                            }
                        }
                    }

                    // Run the Plan
                    if (plan.GetReadOnlyRounds().Any())
                    {
                        await new LPSManager(
                            _logger,
                            _httpClientManager,
                            _config,
                            _watchdog,
                            _runtimeOperationIdProvider,
                            _httpIterationExecutionCommandStatusMonitor,
                            _lpsMonitoringEnroller,
                            _dashboardConfig,
                            _cts
                        ).RunAsync(plan);
                    }
                    else
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId, "No rounds to execute", LPSLoggingLevel.Information);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, $"{ex.Message}\r\n{ex.InnerException?.Message}\r\n{ex.StackTrace}", LPSLoggingLevel.Error);
                }
            },
            LPSRunCommandOptions.ConfigFileArgument,
            LPSRunCommandOptions.RoundNameOption,
            LPSRunCommandOptions.TagOption,
            LPSRunCommandOptions.EnvironmentOption);
        }

        private async Task<VariableHolder> BuildVariableHolder(VariableDto variableDto, bool isGlobal, CancellationToken cancellationToken)
        {
            var mimeType = MimeTypeExtensions.FromKeyword(variableDto.As);
            var builder = new VariableHolder.Builder(_placeholderResolverService);
            return await builder
                .WithFormat(mimeType)
                .WithPattern(variableDto.Regex)
                .WithRawValue(variableDto.Value)
                .SetGlobal(isGlobal)
                .BuildAsync(cancellationToken);
        }
    }
}
