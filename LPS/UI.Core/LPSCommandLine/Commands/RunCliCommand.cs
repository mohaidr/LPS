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
using LPS.Domain.LPSFlow.LPSHandlers;
using LPS.Infrastructure.LPSClients.GlobalVariableManager;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Domain.Common;
using LPS.UI.Core.LPSValidators;
using FluentValidation;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class RunCliCommand : ICliCommand
    {
        readonly Command _rootLpsCliCommand;
        readonly ILogger _logger;
        readonly IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _httpClientManager;
        readonly IClientConfiguration<HttpRequest> _config;
        readonly IVariableManager _variableManager;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IWatchdog _watchdog;
        readonly IPlaceholderResolverService _placeholderResolverService;
        Command _runCommand;
        public Command Command => _runCommand;
        readonly IMetricsDataMonitor _lPSMonitoringEnroller;
        readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandStatusMonitor;
        readonly IOptions<DashboardConfigurationOptions> _dashboardConfig;
        readonly CancellationTokenSource _cts;

        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        internal RunCliCommand(
            Command rootCLICommandLine,
            ILogger logger,
            IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> httpClientManager,
            IClientConfiguration<HttpRequest> config,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IWatchdog watchdog,
            ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandStatusMonitor,
            IMetricsDataMonitor lPSMonitoringEnroller,
            IOptions<DashboardConfigurationOptions> dashboardConfig,
            IVariableManager variableManager,
            IPlaceholderResolverService placeholderResolverService,
            CancellationTokenSource cts)
        {
            _rootLpsCliCommand = rootCLICommandLine;
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
            _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
            _lPSMonitoringEnroller = lPSMonitoringEnroller;
            _dashboardConfig = dashboardConfig;
            _cts = cts;
            _variableManager = variableManager;
            _placeholderResolverService = placeholderResolverService;
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
            _runCommand.SetHandler(async (string configFile, IList<string> roundNames, IList<string> tags, IList<string> environments) =>
            {
                try
                {
                    var planDto = ConfigurationService.FetchConfiguration<PlanDto>(configFile);
                    var plan = new Plan(planDto, _logger, _runtimeOperationIdProvider, _placeholderResolverService);
                    if (plan.IsValid)
                    {
                        var variableValidator = new VariableValidator();
                        var environmentValidator = new EnvironmentValidator();

                        // Add global variables
                        if (planDto?.Variables != null)
                        {
                            foreach (var variable in planDto.Variables)
                            {
                                variableValidator.ValidateAndThrow(variable);

                                MimeType @as = MimeTypeExtensions.FromKeyword(variable.As);
                                var builder = new VariableHolder.Builder(_placeholderResolverService);
                                var variableHolder = await builder
                                    .WithFormat(@as)
                                    .WithPattern(variable.Regex)
                                    .WithRawValue(variable.Value)
                                    .SetGlobal(true)
                                    .BuildAsync(cancellationToken);
                                _variableManager.AddVariableAsync(variable.Name, variableHolder, cancellationToken).Wait();
                            }
                        }

                        // Add environment-specific variables
                        if (planDto?.Environments != null)
                        {
                            foreach (var environmentName in environments)
                            {
                                var environment = planDto.Environments
                                    .FirstOrDefault(env => env.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

                                if (environment != null)
                                {
                                    environmentValidator.ValidateAndThrow(environment);

                                    foreach (var variable in environment.Variables)
                                    {
                                        variableValidator.ValidateAndThrow(variable);

                                        MimeType @as = MimeTypeExtensions.FromKeyword(variable.As);
                                        var builder = new VariableHolder.Builder(_placeholderResolverService);
                                        var variableHolder = await builder
                                            .WithFormat(@as)
                                            .WithPattern(variable.Regex)
                                            .WithRawValue(variable.Value)
                                            .SetGlobal(false) // Environment-specific
                                            .BuildAsync(cancellationToken);

                                        _variableManager.AddVariableAsync(variable.Name, variableHolder, cancellationToken).Wait();
                                    }
                                }
                                else
                                {
                                    _logger.Log(_runtimeOperationIdProvider.OperationId, $"Environment '{environmentName}' not found.", LPSLoggingLevel.Warning);
                                }
                            }
                        }

                        foreach (var roundDto in planDto.Rounds.Where(
                            round => (roundNames.Count == 0 && tags.Count == 0) || 
                            (roundNames.Contains(round.Name, StringComparer.OrdinalIgnoreCase) || round.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))))                        
                        {
                            var roundEntity = new Round(roundDto, _logger, _runtimeOperationIdProvider);
                            if (roundEntity.IsValid)
                            {
                                foreach (var iterationDto in roundDto.Iterations)
                                {
                                    var iterationEntity = new HttpIteration(iterationDto, _logger, _runtimeOperationIdProvider);
                                    if (iterationEntity.IsValid)
                                    {
                                        if (iterationDto.HttpRequest?.URL !=null && roundDto?.BaseUrl != null && !iterationDto.HttpRequest.URL.StartsWith("http://") && !iterationDto.HttpRequest.URL.StartsWith("https://"))
                                        {
                                            if (iterationDto.HttpRequest.URL.StartsWith("$") && roundDto.BaseUrl.StartsWith("$"))
                                            {
                                                throw new InvalidOperationException("Either the base URL or the local URL is defined as a variable, but runtime handling of both as variables is not supported. Consider setting the base URL as a global variable and reusing it in the local variable.");
                                            }
                                            iterationDto.HttpRequest.URL = $"{roundDto.BaseUrl}{iterationDto.HttpRequest.URL}";
                                        }
                                        var request = new HttpRequest(iterationDto.HttpRequest, _logger, _runtimeOperationIdProvider);
                                        if (request.IsValid)
                                        {
                                            if (iterationDto?.HttpRequest?.Capture != null)
                                            {
                                                var capture = new CaptureHandler(iterationDto.HttpRequest.Capture, _logger, _runtimeOperationIdProvider);
                                                if (capture.IsValid)
                                                {
                                                    request.SetCapture(capture);
                                                }
                                            }
                                            iterationEntity.SetHttpRequest(request);
                                            roundEntity.AddIteration(iterationEntity);
                                        }
                                    }
                                }

                                foreach (var referencedIteration in roundDto.ReferencedIterations)
                                {
                                    // Find the referenced iteration by name in the global iterations list
                                    var globalIteration = planDto.Iterations.FirstOrDefault(i => i.Name.Equals(referencedIteration.Name, StringComparison.OrdinalIgnoreCase));
                                    if (globalIteration != null)
                                    {
                                        if (globalIteration.HttpRequest?.URL != null && roundDto?.BaseUrl != null && !globalIteration.HttpRequest.URL.StartsWith("http://") && !globalIteration.HttpRequest.URL.StartsWith("https://"))
                                        {
                                            if (globalIteration.HttpRequest.URL.StartsWith("$") && roundDto.BaseUrl.StartsWith("$"))
                                            {
                                                throw new InvalidOperationException("Either the base URL or the local URL is defined as a variable, but runtime handling of both as variables is not supported. Consider setting the base URL as a global variable and reusing it in the local variable.");
                                            }
                                            globalIteration.HttpRequest.URL = $"{roundDto.BaseUrl}{globalIteration.HttpRequest.URL}";
                                        }
                                        var referencedIterationEntity = new HttpIteration(globalIteration, _logger, _runtimeOperationIdProvider);
                                        if (referencedIterationEntity.IsValid)
                                        {
                                            var request = new HttpRequest(globalIteration.HttpRequest, _logger, _runtimeOperationIdProvider);
                                            if (request.IsValid)
                                            {
                                                if (globalIteration?.HttpRequest?.Capture != null)
                                                {
                                                    var capture = new CaptureHandler(globalIteration.HttpRequest.Capture, _logger, _runtimeOperationIdProvider);
                                                    if (capture.IsValid)
                                                        request.SetCapture(capture);
                                                }
                                                referencedIterationEntity.SetHttpRequest(request);
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
                    else {
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
    }
}
