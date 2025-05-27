using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.GRPCExtensions;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using Microsoft.Extensions.Hosting;
using NodeStatus = LPS.Protos.Shared.NodeStatus;
using NodeType = LPS.Infrastructure.Nodes.NodeType;

namespace LPS.Infrastructure.Services
{
    public class NodeHealthMonitorBackgroundService
    {
        private readonly INodeRegistry _nodeRegistry;
        private readonly IClusterConfiguration _clusterConfiguration;
        private readonly ICustomGrpcClientFactory _grpcClientFactory;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _opIdProvider;
        private readonly CancellationTokenSource _cts;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5); // configurable
        bool _stop = false;

        public NodeHealthMonitorBackgroundService(
            INodeRegistry nodeRegistry,
            IClusterConfiguration clusterConfiguration,
            ICustomGrpcClientFactory grpcClientFactory,
            ILogger logger,
            IRuntimeOperationIdProvider opIdProvider,
            CancellationTokenSource cts)
        {
            _nodeRegistry = nodeRegistry;
            _clusterConfiguration = clusterConfiguration;
            _grpcClientFactory = grpcClientFactory;
            _logger = logger;
            _opIdProvider = opIdProvider;
            _cts = cts;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _stop = false;

            while (!_stop)
            {
                await DoTheCheck(cancellationToken);
                try
                {
                    await Task.Delay(_checkInterval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            await DoTheCheck(cancellationToken);
        }

        public void Stop() { 
            _stop = true;
        }
        private async Task DoTheCheck(CancellationToken token)
        {
            try
            {
                var localNode = _nodeRegistry.GetLocalNode();

                if (localNode.Metadata.NodeType == NodeType.Worker)
                {
                    await CheckMasterAsync(token);
                }
                else if (localNode.Metadata.NodeType == NodeType.Master)
                {
                    await CheckWorkersAsync(token);
                }
            }
            catch (Exception ex)
            {
                _logger.Log(_opIdProvider.OperationId, $"NodeHealthMonitor encountered an error: {ex.Message}", LPSLoggingLevel.Warning);
            }
        }

        private async Task CheckMasterAsync(CancellationToken token)
        {
            var master = _nodeRegistry.GetMasterNode();
            var ip = master?.Metadata.NodeIP ?? _clusterConfiguration.MasterNodeIP;
            var client = _grpcClientFactory.GetClient<GrpcNodeClient>(ip);

            try
            {
                var status = await client.GetNodeStatusAsync(new GetNodeStatusRequest());
                if (status.Status == NodeStatus.Stopped || status.Status == NodeStatus.Failed)
                {
                    _logger.Log(_opIdProvider.OperationId, $"Master reported non-running status: {status.Status}", LPSLoggingLevel.Warning);
                    _cts.Cancel(); // trigger local cancellation
                }
            }
            catch
            {
                _logger.Log(_opIdProvider.OperationId, "Master is unreachable. Cancelling local test.", LPSLoggingLevel.Error);
                await master.SetNodeStatus(Nodes.NodeStatus.Failed);
                _cts.Cancel(); // trigger local cancellation
            }
        }

        private async Task CheckWorkersAsync(CancellationToken token)
        {
            var neighbors = _nodeRegistry.GetNeighborNodes()
                .Where(n => n.NodeStatus == NodeStatus.Running.ToLocal() || n.NodeStatus == NodeStatus.Ready.ToLocal() || n.NodeStatus == NodeStatus.Pending.ToLocal());

            foreach (var worker in neighbors)
            {
                var client = _grpcClientFactory.GetClient<GrpcNodeClient>(worker.Metadata.NodeIP);
                try
                {
                    var status = await client.GetNodeStatusAsync(new GetNodeStatusRequest());
                    await worker.SetNodeStatus(status.Status.ToLocal());
                }
                catch
                {
                    _logger.Log(_opIdProvider.OperationId, $"Worker {worker.Metadata.NodeName} is unreachable. Marking as failed.", LPSLoggingLevel.Warning);
                    await worker.SetNodeStatus(NodeStatus.Failed.ToLocal());
                }
            }
        }
    }
}
