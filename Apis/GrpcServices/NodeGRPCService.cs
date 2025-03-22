using LPS.Infrastructure.Nodes;
using Grpc.Core;
using LPS.Protos.Shared;
using static LPS.Protos.Shared.NodeService;
using Apis.Common;
using LPS.Common.Interfaces;
using LPS.Domain.Common.Interfaces;
namespace Apis.Services
{
    public class NodeGRPCService : NodeServiceBase
    {
        
        private readonly INodeRegistry _nodeRegistry;
        private readonly LPS.Domain.Common.Interfaces.ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly IClusterConfiguration _clusterConfig;
        private readonly ITestTriggerNotifier _testTriggerNotifier;
        private readonly CancellationTokenSource _cts;
        // Assuming we’ll need a way to call workers; could be injected or built from registry

        public NodeGRPCService(
            INodeRegistry nodeRegistry,
            IClusterConfiguration clusterConfig,
            ITestTriggerNotifier testTriggerNotifier,
            LPS.Domain.Common.Interfaces.ILogger logger, 
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            CancellationTokenSource cts)
        {
            _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _clusterConfig = clusterConfig ?? throw new ArgumentNullException(nameof(clusterConfig));
            _testTriggerNotifier = testTriggerNotifier;
            _cts = cts ?? throw new ArgumentNullException(nameof(_cts));
        }

        public override Task<RegisterNodeResponse> RegisterNode(LPS.Protos.Shared.NodeMetadata metadata, ServerCallContext context)
        {
            if (string.IsNullOrWhiteSpace(metadata.NodeName) || string.IsNullOrWhiteSpace(metadata.NodeIp))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "NodeName and NodeIp must be provided."));
            }

            var disks = metadata.Disks.Select(d => new LPS.Infrastructure.Nodes.DiskInfo(d.Name, d.TotalSize, d.FreeSpace)).ToList<IDiskInfo>();
            var networkInterfaces = metadata.NetworkInterfaces.Select(n => new LPS.Infrastructure.Nodes.NetworkInfo(n.InterfaceName, n.Type, n.Status, n.IpAddresses.ToList())).ToList<INetworkInfo>();

            var nodeMetadata = new LPS.Infrastructure.Nodes.NodeMetadata(
                _clusterConfig,
                metadata.NodeName,
                metadata.NodeIp,
                metadata.Os,
                metadata.Architecture,
                metadata.Framework,
                metadata.Cpu,
                metadata.LogicalProcessors,
                metadata.TotalRam,
                disks,
                networkInterfaces
            );

            var node = new Node(nodeMetadata, _clusterConfig, _nodeRegistry);
            _nodeRegistry.RegisterNode(node);

            _logger.Log(_runtimeOperationIdProvider.OperationId, $"Registered Node: {node.Metadata.NodeName} as {node.Metadata.NodeType}", LPSLoggingLevel.Information);

            return Task.FromResult(new RegisterNodeResponse
            {
                Message = $"Node {metadata.NodeName} registered successfully as {metadata.NodeType}"
            });
        }

        public override async Task<GetNodeStatusResponse> GetNodeStatus(GetNodeStatusRequest request, ServerCallContext context)
        {
            try
            {
                var node = _nodeRegistry.FetchLocalNode();

                return new GetNodeStatusResponse
                {
                    Status = ConvertToProtoNodeStatus(node.NodeStatus)
                };
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Failed to fetch node status.");
                throw new RpcException(new Status(StatusCode.Internal, "Failed to retrieve node status."));
            }
        }

        // Master triggers test on worker
        public override async Task<TriggerTestResponse> TriggerTest(TriggerTestRequest request, ServerCallContext context)
        {
            // This method might be called on the worker’s gRPC server
            try
            {
                // Assume the worker starts its local test here
                //_logger.LogInformation("Received TriggerTest request. Starting local test...");

                // Simulate starting the test (replace with actual test logic)
                //_logger.LogInformation("Received TriggerTest request. Notifying CLI to start the test...");

                _testTriggerNotifier.NotifyObservers();

                return new TriggerTestResponse { Status = LPS.Protos.Shared.NodeStatus.Running };
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Failed to start test.");
                return new TriggerTestResponse { Status = LPS.Protos.Shared.NodeStatus.Failed };
            }
        }

        // Worker reports status to master
        public override async Task<SetNodeStatusResponse> SetNodeStatus(SetNodeStatusRequest request, ServerCallContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.NodeName) || string.IsNullOrWhiteSpace(request.NodeIp))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "NodeName and NodeIp must be provided."));
                }

                //_logger.LogInformation($"Received status update from {request.NodeName}: {request.Status}");

                // Update the node status in the registry (assuming INodeRegistry supports this)
                var node = _nodeRegistry.FetchAllNodes(n => n.Metadata.NodeName == request.NodeName && n.Metadata.NodeIP == request.NodeIp).Single();
                if (node == null)
                {
                    return new SetNodeStatusResponse
                    {
                        Success = false,
                        Message = $"Node {request.NodeName} not found in registry."
                    };
                }

                // Update node status (you might need to adjust INodeRegistry/Node to support this)
                node.SetNodeStatus(ConvertToInternalNodeStatus(request.Status));

                return new SetNodeStatusResponse
                {
                    Success = true,
                    Message = $"Status updated for {request.NodeName} to {request.Status}"
                };
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Failed to process status report from {NodeName}.", request.NodeName);
                return new SetNodeStatusResponse
                {
                    Success = false,
                    Message = $"Failed to update status: {ex.Message}"
                };
            }
        }

        // Master cancels test on worker
        public override async Task<CancelTestResponse> CancelTest(CancelTestRequest request, ServerCallContext context)
        {
            try
            {
                //_logger.LogInformation("Received CancelTest request. Cancelling local test...");

                // Simulate cancelling the test (replace with actual cancellation logic)
                bool testCancelled = CancelLocalTest(); // Hypothetical method

                var status = testCancelled ? LPS.Protos.Shared.NodeStatus.Stopped : LPS.Protos.Shared.NodeStatus.Failed;
                return new CancelTestResponse
                {
                    Success = testCancelled,
                    Status = status
                };
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Failed to cancel test.");
                return new CancelTestResponse
                {
                    Success = false,
                    Status = LPS.Protos.Shared.NodeStatus.Failed
                };
            }
        }

        private bool CancelLocalTest()
        {
            // Replace with your logic to cancel the load test on the worker'
            _cts.Cancel();
            return true; // Simulate success
        }

        private LPS.Infrastructure.Nodes.NodeStatus ConvertToInternalNodeStatus(LPS.Protos.Shared.NodeStatus protoStatus)
        {
            // Assuming LPS.Infrastructure.Nodes has its own NodeStatus enum
            return protoStatus switch
            {
                LPS.Protos.Shared.NodeStatus.Running => LPS.Infrastructure.Nodes.NodeStatus.Running,
                LPS.Protos.Shared.NodeStatus.Ready => LPS.Infrastructure.Nodes.NodeStatus.Ready,
                LPS.Protos.Shared.NodeStatus.Stopped => LPS.Infrastructure.Nodes.NodeStatus.Stopped,
                LPS.Protos.Shared.NodeStatus.Failed => LPS.Infrastructure.Nodes.NodeStatus.Failed,
                LPS.Protos.Shared.NodeStatus.Pending => LPS.Infrastructure.Nodes.NodeStatus.Pending,
                _ => throw new NotImplementedException()
            };
        }
        private LPS.Protos.Shared.NodeStatus ConvertToProtoNodeStatus(LPS.Infrastructure.Nodes.NodeStatus internalStatus)
        {
            return internalStatus switch
            {
                LPS.Infrastructure.Nodes.NodeStatus.Running => LPS.Protos.Shared.NodeStatus.Running,
                LPS.Infrastructure.Nodes.NodeStatus.Ready => LPS.Protos.Shared.NodeStatus.Ready,
                LPS.Infrastructure.Nodes.NodeStatus.Stopped => LPS.Protos.Shared.NodeStatus.Stopped,
                LPS.Infrastructure.Nodes.NodeStatus.Failed => LPS.Protos.Shared.NodeStatus.Failed,
                LPS.Infrastructure.Nodes.NodeStatus.Pending => LPS.Protos.Shared.NodeStatus.Pending,
                _ => throw new NotImplementedException()
            };
        }
    }
}