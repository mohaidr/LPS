using AutoMapper;
using FluentValidation;
using LPS.Domain;
using LPS.Domain.Domain.Common.Exceptions;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSFlow.LPSHandlers;
using LPS.UI.Common.DTOs;
using LPS.Infrastructure.Nodes;
using LPS.UI.Core.LPSValidators;
using LPS.Domain.Common.Interfaces;
using LPS.UI.Core.LPSCommandLine;
using LPS.AutoMapper;
using LPS.UI.Core.Host;
using LPS.UI.Common;
using LPS.Infrastructure.Entity;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Extensions;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.MetricsServices;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Globalization;

namespace LPS.UI.Core.Services
{
    public class TestExecutionService : ITestExecutionService
    {
        private readonly ILogger _logger;
        private readonly IClusterConfiguration _clusterConfiguration;
        private readonly INodeRegistry _nodeRegistry;
        private readonly IEntityDiscoveryService _entityDiscoveryService;
        private readonly IMapper _mapper;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly IPlaceholderResolverService _placeholderResolverService;
        private readonly IVariableManager _variableManager;
        private readonly IMetricsDataMonitor _lpsMonitoringEnroller;
        private readonly IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _httpClientManager;
        private readonly IClientConfiguration<HttpRequest> _config;
        private readonly IWatchdog _watchdog;
        private readonly ICommandStatusMonitor<HttpIteration> _httpIterationExecutionCommandStatusMonitor;
        private readonly IDashboardService _dashboardService;
        private readonly CancellationTokenSource _cts;
        private readonly IEntityRepositoryService _entityRepositoryService;
        private readonly ICustomGrpcClientFactory _customGrpcClientFactory;
        private readonly IIterationStatusMonitor _iterationStatusMonitor;
        public readonly ICommandRepository<HttpIteration, IAsyncCommand<HttpIteration>> _httpIterationExecutionCommandRepository;
        public readonly ISkipIfEvaluator _skipIfEvaluator;
        public readonly IVariableFactory _variableFactory;
        private readonly INodeMetadata _nodeMetaData;
        private readonly ILiveMetricDataStore _metricStore;
        private readonly IWindowedMetricDataStore _windowedMetricStore;
        private readonly ICumulativeMetricDataStore _cumulativeMetricStore;
        private readonly IWarmUpService _warmupService;
        private readonly IWindowedMetricsCoordinator _windowedMetricsCoordinator;
        private readonly ICumulativeMetricsCoordinator _cumulativeMetricsCoordinator;
        private readonly IPlanExecutionContext _planContext;
        public TestExecutionService(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IClusterConfiguration clusterConfiguration,
            INodeRegistry nodeRegistry,
            IEntityDiscoveryService entityDiscoveryService,
            IPlaceholderResolverService placeholderResolverService,
            IVariableManager variableManager,
            IMetricsDataMonitor lpsMonitoringEnroller,
            IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> httpClientManager,
            IClientConfiguration<HttpRequest> config,
            IWatchdog watchdog,
            IDashboardService dashboardService,
            ICommandStatusMonitor<HttpIteration> httpIterationExecutionCommandStatusMonitor,
            IEntityRepositoryService entityRepositoryService,
            ICustomGrpcClientFactory customGrpcClientFactory,
            IIterationStatusMonitor iterationStatusMonitor,
            ISkipIfEvaluator skipIfEvaluator,
            IVariableFactory variableFactory,
            ILiveMetricDataStore metricStore,             // NEW
            IWindowedMetricDataStore windowedMetricStore, // NEW
            ICumulativeMetricDataStore cumulativeMetricStore, // NEW
            INodeMetadata nodeMetaData,               // NEW
            IWindowedMetricsCoordinator windowedMetricsCoordinator,  // NEW: Windowed metrics coordinator
            ICumulativeMetricsCoordinator cumulativeMetricsCoordinator,  // NEW: Cumulative metrics coordinator
            IPlanExecutionContext planContext,
            ICommandRepository<HttpIteration, IAsyncCommand<HttpIteration>> httpIterationExecutionCommandRepository,
            IWarmUpService warmUpService,
            CancellationTokenSource cts)
        {
            _logger = logger;
            _clusterConfiguration = clusterConfiguration;
            _nodeRegistry = nodeRegistry;
            _entityDiscoveryService = entityDiscoveryService;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _placeholderResolverService = placeholderResolverService;
            _variableManager = variableManager;
            _lpsMonitoringEnroller = lpsMonitoringEnroller;
            _httpClientManager = httpClientManager;
            _config = config;
            _watchdog = watchdog;
            _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
            _dashboardService = dashboardService;
            _cts = cts;
            _entityRepositoryService = entityRepositoryService;
            _customGrpcClientFactory = customGrpcClientFactory;
            _iterationStatusMonitor = iterationStatusMonitor;
            _skipIfEvaluator = skipIfEvaluator;
            _variableFactory = variableFactory;
            _metricStore = metricStore;
            _windowedMetricStore = windowedMetricStore;
            _cumulativeMetricStore = cumulativeMetricStore;
            _nodeMetaData = nodeMetaData;
            _warmupService = warmUpService;
            _windowedMetricsCoordinator = windowedMetricsCoordinator;
            _cumulativeMetricsCoordinator = cumulativeMetricsCoordinator;
            _planContext = planContext;
            _httpIterationExecutionCommandRepository = httpIterationExecutionCommandRepository;

            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new DtoToCommandProfile(placeholderResolverService, string.Empty));
            });
            mapperConfig.AssertConfigurationIsValid();
            _mapper = mapperConfig.CreateMapper();
        }

        public async Task ExecuteAsync(TestRunParameters parameters)
        {
            var localNode = _nodeRegistry.GetLocalNode();
            var planDto = parameters.IsInline
                ? parameters.PlanDto
                : ConfigurationService.FetchConfiguration<PlanDto>(parameters.ConfigFile, _placeholderResolverService);

            // --- PLAN VALIDATION ---
            var planValidator = new PlanValidator(planDto);
            var planResults = planValidator.Validate(planDto);
            if (!planResults.IsValid)
            {
                planResults.PrintValidationErrors(_logger);
                return;
            }

            var planCommand = _mapper.Map<Plan.SetupCommand>(planDto);
            var plan = new Plan(planCommand, _logger, _runtimeOperationIdProvider, _placeholderResolverService);

            // Set plan execution context for metrics
            var testStartTime = DateTime.UtcNow;
            _planContext.SetContext(planDto.Name ?? "unknown", testStartTime);

            if (plan.IsValid)
            {
                var variableValidator = new VariableValidator();
                var environmentValidator = new EnvironmentValidator();

                // --- GLOBAL VARIABLES ---
                foreach (var variableDto in planDto.Variables)
                {
                    var varResults = variableValidator.Validate(variableDto);
                    if (!varResults.IsValid)
                    {
                        varResults.PrintValidationErrors(_logger);
                        return;
                    }

                    var variableHolder = await BuildVariableHolder(variableDto, true, parameters.CancellationToken);
                    _variableManager.PutAsync(variableDto.Name, variableHolder, parameters.CancellationToken).Wait();
                }

                // --- ENVIRONMENT-SPECIFIC VARIABLES ---
                foreach (var environmentName in parameters.Environments)
                {
                    var environmentDto = planDto.Environments
                        .FirstOrDefault(env => env.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

                    if (environmentDto != null)
                    {
                        var envResults = environmentValidator.Validate(environmentDto);
                        if (!envResults.IsValid)
                        {
                            envResults.PrintValidationErrors(_logger);
                            return;
                        }

                        foreach (var variableDto in environmentDto.Variables)
                        {
                            var varResults = variableValidator.Validate(variableDto);
                            if (!varResults.IsValid)
                            {
                                varResults.PrintValidationErrors(_logger);
                                return;
                            }
                            var variableHolder = await BuildVariableHolder(variableDto, false, parameters.CancellationToken);
                            _variableManager.PutAsync(variableDto.Name, variableHolder, parameters.CancellationToken).Wait();
                        }
                    }
                    else
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId,
                            $"Environment '{environmentName}' not found.",
                            LPSLoggingLevel.Warning);
                    }
                }

                // Rounds and Iterations
                foreach (var roundDto in planDto.Rounds.Where(round =>
                    parameters.RoundNames.Count == 0 && parameters.Tags.Count == 0 ||
                    parameters.RoundNames.Contains(round.Name, StringComparer.OrdinalIgnoreCase) ||
                    round.Tags.Any(tag => parameters.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))))
                {
                    var roundCommand = _mapper.Map<Round.SetupCommand>(roundDto);
                    var roundEntity = new Round(roundCommand, _logger, _lpsMonitoringEnroller, _runtimeOperationIdProvider);

                    var roundValidator = new RoundValidator(roundDto);
                    var roundResults = roundValidator.Validate();
                    if (!roundResults.IsValid)
                    {
                        roundResults.PrintValidationErrors();
                        return;
                    }

                    if (roundEntity.IsValid)
                    {
                        foreach (var iterationDto in roundDto.Iterations)
                        {
                            var iterationEntity = ProcessIteration(roundDto, iterationDto);
                            if (iterationEntity.IsValid)
                            {
                                roundEntity.AddIteration(iterationEntity);
                            }
                        }

                        foreach (var referencedIteration in roundDto.ReferencedIterations)
                        {
                            var globalIteration = planDto.Iterations
                                .FirstOrDefault(i => i.Name.Equals(referencedIteration, StringComparison.OrdinalIgnoreCase));
                            if (globalIteration != null)
                            {
                                var iterationEntity = ProcessIteration(roundDto, globalIteration);
                                if (iterationEntity.IsValid)
                                {
                                    roundEntity.AddIteration(iterationEntity);
                                }
                            }
                            else
                            {
                                _logger.Log(_runtimeOperationIdProvider.OperationId,
                                    $"Referenced iteration '{referencedIteration}' not found.",
                                    LPSLoggingLevel.Warning);
                            }
                        }

                        plan.AddRound(roundEntity);
                    }
                }
            }

            if (plan.GetReadOnlyRounds().Any())
            {
                var hosts = plan.GetReadOnlyRounds().SelectMany(r => r.GetReadOnlyIterations().Select(iteration =>  ((HttpIteration)iteration).HttpRequest.Url.BaseUrl));
                await _warmupService.TryWarmUpAsync(hosts.Distinct(), ct: parameters.CancellationToken);
                
                await RegisterEntities(plan);
                await localNode.SetNodeStatus(NodeStatus.Running);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Plan '{plan?.Name}' execution has started", LPSLoggingLevel.Information);
                _dashboardService.Start();
                
                await new Plan.ExecuteCommand(_logger, _watchdog, _runtimeOperationIdProvider, _httpClientManager,
                        _config, _httpIterationExecutionCommandStatusMonitor,
                        _httpIterationExecutionCommandRepository, _lpsMonitoringEnroller, _iterationStatusMonitor)
                    .ExecuteAsync(plan, _cts.Token);
                
                // Coordinators are stopped in HostedService.StopAsync after waiting for all workers to complete
                
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Plan '{plan?.Name}' execution has completed", LPSLoggingLevel.Information);
                await PersistAllSnapshotsAsync(plan, _cts.Token);
            }
            else
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, "No rounds to execute", LPSLoggingLevel.Information);
            }
        }

        // unchanged methods ...
        private async Task<IVariableHolder> BuildVariableHolder(
            VariableDto variableDto,
            bool isGlobal,
            CancellationToken cancellationToken)
        {
            var variableType = variableDto.As.ToVariableType();

            switch (variableType)
            {
                case VariableType.String:
                case VariableType.QString:
                case VariableType.JsonString:
                case VariableType.XmlString:
                case VariableType.CsvString:
                case VariableType.QJsonString:
                case VariableType.QXmlString:
                case VariableType.QCsvString:
                    return await _variableFactory.CreateStringAsync(
                        variableDto.Value,
                        variableType,
                        variableDto.Regex,
                        true,
                        cancellationToken);

                case VariableType.Int:
                case VariableType.Double:
                case VariableType.Float:
                case VariableType.Decimal:
                    return await _variableFactory.CreateNumberAsync(
                        variableDto.Value,
                        variableType,
                        true,
                        cancellationToken);

                case VariableType.Boolean:
                    return await _variableFactory.CreateBooleanAsync(
                        variableDto.Value,
                        true,
                        cancellationToken);

                default:
                    throw new NotImplementedException(
                        $"Variable type '{variableType}' is not yet supported in {nameof(BuildVariableHolder)}.");
            }
        }

        private HttpIteration ProcessIteration(RoundDto roundDto, HttpIterationDto iterationDto)
        {
            if (iterationDto.HttpRequest?.URL != null &&
                roundDto?.BaseUrl != null &&
                !iterationDto.HttpRequest.URL.StartsWith("http://") &&
                !iterationDto.HttpRequest.URL.StartsWith("https://"))
            {
                if (iterationDto.HttpRequest.URL.StartsWith("$") && roundDto.BaseUrl.StartsWith("$"))
                {
                    throw new InvalidOperationException(
                        "Either the base URL or the local URL is defined as a variable, but runtime handling of both as variables is not supported. " +
                        "Consider setting the base URL as a global variable and reusing it in the local variable.");
                }
                iterationDto.HttpRequest.URL = $"{roundDto.BaseUrl}{iterationDto.HttpRequest.URL}";
            }

            var iterationCommand = _mapper.Map<HttpIteration.SetupCommand>(iterationDto);
            var iterationEntity = new HttpIteration(iterationCommand, _skipIfEvaluator, _logger, _runtimeOperationIdProvider);

            if (iterationEntity.IsValid)
            {
                var requestCommand = _mapper.Map<HttpRequest.SetupCommand>(iterationDto.HttpRequest);
                var request = new HttpRequest(requestCommand, _logger, _runtimeOperationIdProvider);
                if (request.IsValid)
                {
                    if (iterationDto?.HttpRequest?.Capture != null)
                    {
                        var captureCommand = _mapper.Map<CaptureHandler.SetupCommand>(iterationDto.HttpRequest.Capture);
                        var capture = new CaptureHandler(captureCommand, _logger, _runtimeOperationIdProvider);
                        if (capture.IsValid)
                        {
                            request.SetCapture(capture);
                        }
                        else
                        {
                            throw new InvalidLPSEntityException(
                                $"Invalid Capture handler defined in the iteration {iterationDto.Name}, Please fix the validation errors and try again");
                        }
                    }
                    iterationEntity.SetHttpRequest(request);
                }
                else
                {
                    throw new InvalidLPSEntityException(
                        $"Invalid HttpRequest in the iteration {iterationDto.Name}, Please fix the validation errors and try again");
                }
                return iterationEntity;
            }
            throw new InvalidLPSEntityException(
                $"Invalid Iteration {iterationDto.Name}, Please fix the validation errors and try again");
        }

        private async ValueTask RegisterEntities(Plan plan)
        {
            var entityRegisterer = new EntityRegisterer(
                _clusterConfiguration,
                _nodeRegistry.GetLocalNode().Metadata,
                _entityDiscoveryService,
                _nodeRegistry,
                _entityRepositoryService,
                _customGrpcClientFactory);
           await entityRegisterer.RegisterEntitiesAsync(plan);
        }

        private async Task PersistAllSnapshotsAsync(Plan plan, CancellationToken _)
        {
            if (_nodeMetaData.NodeType != NodeType.Master) return;

            var deadline = DateTime.UtcNow.AddSeconds(180);
            using var persistCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var persistToken = persistCts.Token;

            try
            {
                var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss_zzz", CultureInfo.InvariantCulture)
                                              .Replace(":", "");
                var root = Path.Combine("Metrics", $"{plan.Name}_{stamp}");
                Directory.CreateDirectory(root);

                var jsonOpts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new JsonStringEnumConverter() }
                };

                foreach (var round in plan.GetReadOnlyRounds())
                {
                    if (DateTime.UtcNow >= deadline) break;

                    var roundDir = Path.Combine(root, SanitizeFileName(round.Name));
                    Directory.CreateDirectory(roundDir);

                    foreach (var iter in round.GetReadOnlyIterations())
                    {
                        if (DateTime.UtcNow >= deadline) break;

                        // Save cumulative metrics from the cumulative store
                        if (_cumulativeMetricStore.TryGet(iter.Id, out var cumulativeSnaps) && cumulativeSnaps.Count > 0)
                        {
                            // Extract Throughput metrics from all cumulative snapshots
                            var throughputHistory = cumulativeSnaps
                                .Where(s => s.Throughput != null)
                                .Select(s => new 
                                {
                                    s.Timestamp,
                                    s.ExecutionStatus,
                                    s.IsFinal,
                                    Metric = s.Throughput
                                })
                                .ToList();
                            if (throughputHistory.Count > 0)
                            {
                                var file = Path.Combine(roundDir, $"{SanitizeFileName(iter.Name)}_Throughput.json");
                                var json = JsonSerializer.Serialize(throughputHistory, jsonOpts);
                                await File.WriteAllTextAsync(file, json, persistToken);
                            }

                            // Extract Duration/Time metrics from all cumulative snapshots
                            var durationHistory = cumulativeSnaps
                                .Where(s => s.Duration != null)
                                .Select(s => new 
                                {
                                    s.Timestamp,
                                    s.ExecutionStatus,
                                    s.IsFinal,
                                    Metric = s.Duration
                                })
                                .ToList();
                            if (durationHistory.Count > 0)
                            {
                                var file = Path.Combine(roundDir, $"{SanitizeFileName(iter.Name)}_Time.json");
                                var json = JsonSerializer.Serialize(durationHistory, jsonOpts);
                                await File.WriteAllTextAsync(file, json, persistToken);
                            }

                            // Extract DataTransmission metrics from all cumulative snapshots
                            var dataTransmissionHistory = cumulativeSnaps
                                .Where(s => s.DataTransmission != null)
                                .Select(s => new 
                                {
                                    s.Timestamp,
                                    s.ExecutionStatus,
                                    s.IsFinal,
                                    Metric = s.DataTransmission
                                })
                                .ToList();
                            if (dataTransmissionHistory.Count > 0)
                            {
                                var file = Path.Combine(roundDir, $"{SanitizeFileName(iter.Name)}_DataTransmission.json");
                                var json = JsonSerializer.Serialize(dataTransmissionHistory, jsonOpts);
                                await File.WriteAllTextAsync(file, json, persistToken);
                            }

                            // Extract ResponseCode metrics from all cumulative snapshots
                            var responseCodeHistory = cumulativeSnaps
                                .Where(s => s.ResponseCodes != null)
                                .Select(s => new 
                                {
                                    s.Timestamp,
                                    s.ExecutionStatus,
                                    s.IsFinal,
                                    Metric = s.ResponseCodes
                                })
                                .ToList();
                            if (responseCodeHistory.Count > 0)
                            {
                                var file = Path.Combine(roundDir, $"{SanitizeFileName(iter.Name)}_ResponseCode.json");
                                var json = JsonSerializer.Serialize(responseCodeHistory, jsonOpts);
                                await File.WriteAllTextAsync(file, json, persistToken);
                            }
                        }
                        
                        // Save windowed metrics for this iteration - split by metric type like cumulative
                        if (DateTime.UtcNow < deadline && _windowedMetricStore.TryGet(iter.Id, out var windowedSnaps) && windowedSnaps.Count > 0)
                        {
                            // Extract Throughput metrics from all windows
                            var throughputWindows = windowedSnaps
                                .Where(s => s.Throughput != null)
                                .Select(s => new 
                                {
                                    s.WindowSequence,
                                    s.WindowStart,
                                    s.WindowEnd,
                                    Metric = s.Throughput
                                })
                                .ToList();
                            if (throughputWindows.Count > 0)
                            {
                                var file = Path.Combine(roundDir, $"{SanitizeFileName(iter.Name)}_Windowed_Throughput.json");
                                var json = JsonSerializer.Serialize(throughputWindows, jsonOpts);
                                await File.WriteAllTextAsync(file, json, persistToken);
                            }

                            // Extract Duration/Time metrics from all windows
                            var durationWindows = windowedSnaps
                                .Where(s => s.Duration != null)
                                .Select(s => new 
                                {
                                    s.WindowSequence,
                                    s.WindowStart,
                                    s.WindowEnd,
                                    Metric = s.Duration
                                })
                                .ToList();
                            if (durationWindows.Count > 0)
                            {
                                var file = Path.Combine(roundDir, $"{SanitizeFileName(iter.Name)}_Windowed_Time.json");
                                var json = JsonSerializer.Serialize(durationWindows, jsonOpts);
                                await File.WriteAllTextAsync(file, json, persistToken);
                            }

                            // Extract DataTransmission metrics from all windows
                            var dataTransmissionWindows = windowedSnaps
                                .Where(s => s.DataTransmission != null)
                                .Select(s => new 
                                {
                                    s.WindowSequence,
                                    s.WindowStart,
                                    s.WindowEnd,
                                    Metric = s.DataTransmission
                                })
                                .ToList();
                            if (dataTransmissionWindows.Count > 0)
                            {
                                var file = Path.Combine(roundDir, $"{SanitizeFileName(iter.Name)}_Windowed_DataTransmission.json");
                                var json = JsonSerializer.Serialize(dataTransmissionWindows, jsonOpts);
                                await File.WriteAllTextAsync(file, json, persistToken);
                            }

                            // Extract ResponseCode metrics from all windows
                            var responseCodeWindows = windowedSnaps
                                .Where(s => s.ResponseCodes != null)
                                .Select(s => new 
                                {
                                    s.WindowSequence,
                                    s.WindowStart,
                                    s.WindowEnd,
                                    Metric = s.ResponseCodes
                                })
                                .ToList();
                            if (responseCodeWindows.Count > 0)
                            {
                                var file = Path.Combine(roundDir, $"{SanitizeFileName(iter.Name)}_Windowed_ResponseCode.json");
                                var json = JsonSerializer.Serialize(responseCodeWindows, jsonOpts);
                                await File.WriteAllTextAsync(file, json, persistToken);
                            }
                        }
                    }
                }

                if (persistCts.IsCancellationRequested || DateTime.UtcNow >= deadline)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                        $"Metrics persistence timed out after 30s. Partial results saved under '{root}'.",
                        LPSLoggingLevel.Warning);
                }
                else
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                        $"Metrics persisted under '{root}'.",
                        LPSLoggingLevel.Information);
                }
            }
            catch (OperationCanceledException)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    "Metrics persistence canceled by 30s timeout. Partial results saved.",
                    LPSLoggingLevel.Warning);
            }
            catch (Exception ex)
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId,
                    $"Failed to persist metrics snapshots: {ex}",
                    LPSLoggingLevel.Error);
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
