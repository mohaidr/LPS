using Grpc.Core;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Nodes;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using Spectre.Console;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    public class MetricsDataMonitor(
        ILogger logger,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        IMetricAggregatorFactory aggregatorFactory,                // CHANGED
        ICommandStatusMonitor<HttpIteration> commandStatusMonitor,
        INodeMetadata nodeMetadata,
        ICustomGrpcClientFactory customGrpcClientFactory,
        IClusterConfiguration clusterConfiguration,
        IEntityDiscoveryService entityDiscoveryService) : IMetricsDataMonitor, IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IRuntimeOperationIdProvider _op = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
        private readonly IMetricAggregatorFactory _factory = aggregatorFactory ?? throw new ArgumentNullException(nameof(aggregatorFactory));
        private readonly ICommandStatusMonitor<HttpIteration> _commandStatusMonitor = commandStatusMonitor ?? throw new ArgumentNullException(nameof(commandStatusMonitor));
        private readonly INodeMetadata _nodeMetadata = nodeMetadata ?? throw new ArgumentNullException(nameof(nodeMetadata));
        private readonly ICustomGrpcClientFactory _customGrpcClientFactory = customGrpcClientFactory ?? throw new ArgumentNullException(nameof(customGrpcClientFactory));
        private readonly IClusterConfiguration _clusterConfiguration = clusterConfiguration ?? throw new ArgumentNullException(nameof(clusterConfiguration));
        private readonly IEntityDiscoveryService _entityDiscoveryService = entityDiscoveryService ?? throw new ArgumentNullException(nameof(entityDiscoveryService));

        public bool TryRegister(string roundName, HttpIteration httpIteration)
        {
            try
            {
                if (_factory.TryGet(httpIteration.Id, out _))
                {
                    _logger.Log(_op.OperationId, $"Iteration already registered.\nRound: {roundName}\nIteration: {httpIteration.Name}", LPSLoggingLevel.Verbose);
                    return false;
                }

                _ = _factory.GetOrCreate(httpIteration, roundName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Log(_op.OperationId, $"Failed to register iteration.\nRound: {roundName}\nIteration: {httpIteration.Name}\nException: {ex.Message} {ex.InnerException?.Message}", LPSLoggingLevel.Error);
                throw;
            }
        }

        public void Monitor(Func<HttpIteration, bool> predicate)
        {
            var matches = _factory.Iterations.Where(predicate).ToList();
            foreach (var it in matches) Monitor(it);
        }

        public async void Monitor(HttpIteration httpIteration)
        {
            if (!_factory.TryGet(httpIteration.Id, out var aggregators))
            {
                _logger.Log(_op.OperationId, $"Monitoring can't start. Iteration {httpIteration.Name} is not registered.", LPSLoggingLevel.Error);
                return;
            }

            // Ensure master is monitoring (from worker)
            if (_nodeMetadata.NodeType == NodeType.Worker)
            {
                var monitorClient = _customGrpcClientFactory.GetClient<GrpcMonitorClient>(_clusterConfiguration.MasterNodeIP);
                var fqdn = _entityDiscoveryService.Discover(r => r.IterationId == httpIteration.Id).Single().FullyQualifiedName;
                try
                {
                    _logger.Log(_op.OperationId, $"Sending Monitor request for {fqdn}", LPSLoggingLevel.Information);
                    await monitorClient.MonitorAsync(fqdn).ConfigureAwait(false); // CHANGED: async/await
                }
                catch (InvalidOperationException invalidOpEx) when (invalidOpEx.InnerException is RpcException rpcEx)
                {
                    _logger.Log(_op.OperationId, $"{rpcEx.Status}\n{rpcEx.Message}\n{rpcEx.InnerException} {rpcEx.StackTrace}", LPSLoggingLevel.Error);
                    AnsiConsole.MarkupLine($"[Red][[Error]] {DateTime.Now} {rpcEx.Status}\n{rpcEx.Message}[/]");
                }
                catch (Exception ex)
                {
                    _logger.Log(_op.OperationId, $"{ex.Message}\n\t{ex.InnerException}", LPSLoggingLevel.Error);
                    throw;
                }
            }

            foreach (var metric in aggregators) metric.Start();
        }

        public async void Stop(HttpIteration httpIteration)
        {
            if (_factory.TryGet(httpIteration.Id, out var aggregators))
            {
                bool anyOngoing = await _commandStatusMonitor.IsAnyCommandOngoing(httpIteration);
                if (!anyOngoing)
                {
                    foreach (var a in aggregators) a.Stop();
                }
            }
        }

        public async void Stop(Func<HttpIteration, bool> predicate)
        {
            var matches = _factory.Iterations.Where(predicate).ToList();
            foreach (var it in matches) await Task.Run(() => Stop(it));
        }

        public void Dispose() => _factory.Clear(dispose: true);
    }
}
