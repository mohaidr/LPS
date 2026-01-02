using LPS.UI.Common;
using LPS.UI.Core.Build.Services;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSValidators;
using LPS.Infrastructure.Common;
using Spectre.Console;
using LPS.UI.Core.LPSCommandLine;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.UI.Common.DTOs;
using LPS.Infrastructure.Nodes;
using Grpc.Net.Client;
using LPS.Protos.Shared;
using LPS.Common.Interfaces;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.GRPCClients;
using Grpc.Core;
using System.Diagnostics;
using System.Text.RegularExpressions;
using LPS.Infrastructure.Grpc;
using LPS.Infrastructure.Services;
using NodeType = LPS.Infrastructure.Nodes.NodeType;
using Node = LPS.Infrastructure.Nodes.Node;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.Infrastructure.Monitoring.Cumulative;

namespace LPS.UI.Core.Host
{
    internal class HostedService(
        dynamic command_args,
        NodeHealthMonitorBackgroundService nodeHealthMonitorBackgroundService,
        IDashboardService dashboardService,
        ICustomGrpcClientFactory customGrpcClientFactory,
        IClusterConfiguration clusterConfiguration,
        ITestOrchestratorService testOrchestratorService,
        IEntityDiscoveryService entityDiscoveryService,
        INodeMetadata nodeMetadata,
        INodeRegistry nodeRegistry,
        ILogger logger,
        IClientConfiguration<HttpRequest> config,
        IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> httpClientManager,
        IWatchdog watchdog,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        IMetricsDataMonitor metricDataMonitor,
        IVariableManager variableManager,
        IPlaceholderResolverService placeholderResolverService,
        ICommandStatusMonitor<HttpIteration> httpIterationExecutionCommandStatusMonitor,
        AppSettingsWritableOptions appSettings,
        ITestTriggerNotifier testTriggerNotifier,
        ITestExecutionService testExecutionService,
        IWindowedMetricsCoordinator windowedMetricsCoordinator,
        ICumulativeMetricsCoordinator cumulativeMetricsCoordinator,
        CancellationTokenSource cts) : IHostedService
    {
        readonly NodeHealthMonitorBackgroundService _nodeHealthMonitorBackgroundService = nodeHealthMonitorBackgroundService;
        readonly ICustomGrpcClientFactory _customGrpcClientFactory= customGrpcClientFactory;
        private readonly IDashboardService _dashboardService = dashboardService;
        readonly IClusterConfiguration _clusterConfiguration = clusterConfiguration;
        readonly INodeMetadata _nodeMetadata = nodeMetadata;
        readonly ITestTriggerNotifier _testTriggerNotifier = testTriggerNotifier;
        readonly INodeRegistry _nodeRegistry = nodeRegistry;
        readonly ILogger _logger = logger;
        readonly IEntityDiscoveryService _entityDiscoveryService = entityDiscoveryService;
        readonly IClientConfiguration<HttpRequest> _config = config;
        readonly IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _httpClientManager = httpClientManager;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider;
        readonly IWatchdog _watchdog = watchdog;
        readonly AppSettingsWritableOptions _appSettings = appSettings;
        readonly IMetricsDataMonitor _metricDataMonitor = metricDataMonitor;
        readonly ICommandStatusMonitor<HttpIteration> _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
        readonly IVariableManager _variableManager = variableManager;
        readonly IPlaceholderResolverService _placeholderResolverService = placeholderResolverService;
        readonly ITestExecutionService _testExecutionService = testExecutionService;
        readonly ITestOrchestratorService _testOrchestratorService = testOrchestratorService;
        readonly IWindowedMetricsCoordinator _windowedMetricsCoordinator = windowedMetricsCoordinator;
        readonly ICumulativeMetricsCoordinator _cumulativeMetricsCoordinator = cumulativeMetricsCoordinator;
        readonly string[] _command_args = command_args.args;
        readonly CancellationTokenSource _cts = cts;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has started  --------------", LPSLoggingLevel.Verbose);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"is the correlation Id of this run", LPSLoggingLevel.Information);
                
                // Start metrics coordinators (they fire events for collectors to push data)
                await _windowedMetricsCoordinator.StartAsync(_cts.Token);
                await _cumulativeMetricsCoordinator.StartAsync(_cts.Token);

                #pragma warning disable CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
                Console.CancelKeyPress += CancelKeyPressHandler;
                _ = WatchForCancellationAsync();

                if (_command_args != null && _command_args.Length > 0)
                {
                    var commandLineManager = new CommandLineManager(_command_args, _testOrchestratorService, _testExecutionService, _nodeRegistry, _clusterConfiguration, _entityDiscoveryService, _testTriggerNotifier, _logger, _httpClientManager, _config, _watchdog, _runtimeOperationIdProvider, _appSettings, _httpIterationExecutionCommandStatusMonitor, _metricDataMonitor, _variableManager, _placeholderResolverService, _cts);
                    await commandLineManager.RunAsync(_cts.Token);
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Command execution has completed", LPSLoggingLevel.Verbose, cancellationToken);
                }
                else
                {
                    PlanDto planDto = new();
                    var manualBuild = new ManualBuild(new PlanValidator(planDto), _logger, _runtimeOperationIdProvider, _placeholderResolverService);
                    var plan = manualBuild.Build(ref planDto);
                    SavePlanToDisk(planDto);
                    AnsiConsole.MarkupLine($"[bold italic]You can use the command [blue]lps run {planDto.Name}.yaml[/] to execute the Plan[/]");

                }
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, " -------------- LPS V1 - App execution has completed  --------------", LPSLoggingLevel.Verbose, cancellationToken);
                await _logger.FlushAsync();
            }
            catch
            {
                await _nodeRegistry.GetLocalNode()
                .SetNodeStatus(Infrastructure.Nodes.NodeStatus.Failed);
            }
        }

       
        private static void SavePlanToDisk(PlanDto planDto)
        {
            var jsonContent = SerializationHelper.Serialize(planDto);
            File.WriteAllText($"{planDto.Name}.json", jsonContent);

            var yamlContent = SerializationHelper
                .SerializeToYaml(planDto);
            File.WriteAllText($"{planDto.Name}.yaml", yamlContent);

        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _logger.FlushAsync();
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "--------------  LPS V1 - App Exited  --------------", LPSLoggingLevel.Verbose, cancellationToken);
            _programCompleted = true;

            _nodeRegistry.TryGetLocalNode(out INode? localNode); 

            if (localNode?.Metadata.NodeType == NodeType.Master)
            {
                // Wait for all workers to complete before stopping coordinators
                while (_nodeRegistry.GetNeighborNodes().Where(n => n.IsActive()).Count() > 0)
                {
                    await Task.Delay(500);
                }
            }
            
            // Stop coordinators - they fire final events so collectors push final state
            await _windowedMetricsCoordinator.StopAsync(_cts.Token);
            await _cumulativeMetricsCoordinator.StopAsync(_cts.Token);
            
            if(localNode!=null)
                await localNode.SetNodeStatus(Infrastructure.Nodes.NodeStatus.Stopped);

            _nodeHealthMonitorBackgroundService.Stop();

            await _dashboardService.EnsureDashboardUpdateBeforeExitAsync();
        }

        private void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            if (e.SpecialKey == ConsoleSpecialKey.ControlC
                || e.SpecialKey == ConsoleSpecialKey.ControlBreak)
            {
                e.Cancel = true; // Prevent default process termination.
                AnsiConsole.MarkupLine("[yellow]Graceful shutdown requested (Ctrl+C/Break).[/]");
                RequestCancellation(); // Cancel the CancellationTokenSource.
                if (_nodeMetadata.NodeType == Infrastructure.Nodes.NodeType.Master)
                {
                    foreach (var node in _nodeRegistry.Query(n => n.Metadata.NodeType == Infrastructure.Nodes.NodeType.Worker 
                    && n.IsActive()))
                    {
                        try
                        {
                            var client = _customGrpcClientFactory.GetClient<GrpcNodeClient>(node.Metadata.NodeIP);
                            client.CancelTest(new CancelTestRequest());
                        }
                        catch (RpcException ex)
                        {
                         //   node.SetNodeStatus(Infrastructure.Nodes.NodeStatus.Failed);
                            AnsiConsole.MarkupLine($"[red]Unexpected error when sending CancelTest to node {node.Metadata.NodeIP}: {ex.Message}[/]");
                            break;
                        }
                        catch {
                            throw;
                        }
                    }
                }
            }
        }

        static bool _programCompleted;
        private async Task WatchForCancellationAsync()
        {
            while (!_cts.IsCancellationRequested && !_programCompleted)
            {
                if (Console.KeyAvailable) // Check for the Escape key
                {
                    var keyInfo = Console.ReadKey(true);
                    if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        AnsiConsole.MarkupLine("[yellow]Graceful shutdown requested (Escape).[/]");
                        RequestCancellation(); // Cancel the CancellationTokenSource.
                        break; // Exit the loop
                    }
                }
                await Task.Delay(1000); // Poll every second
            }
        }

        private void RequestCancellation()
        {
            AnsiConsole.MarkupLine("[yellow]Gracefully shutting down the LPS local server[/]");
            _cts.Cancel();
        }

    }
}
