using LPS.Infrastructure.Nodes;
using Grpc.Core;
using LPS.Protos.Shared;
using static LPS.Protos.Shared.NodeService;
using LPS.Common.Interfaces;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.GRPCExtensions;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Domain;
using System.Xml.Linq;
using LPS.Infrastructure.Grpc;
using NodeType = LPS.Infrastructure.Nodes.NodeType;
namespace Apis.Services
{
    public class NodeGRPCService : NodeServiceBase
    {
        
        private readonly INodeRegistry _nodeRegistry;
        private readonly LPS.Domain.Common.Interfaces.ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly IClusterConfiguration _clusterConfig;
        private readonly ITestTriggerNotifier _testTriggerNotifier;
        ICustomGrpcClientFactory _customGrpcClientFactory;
        private readonly CancellationTokenSource _cts;

        public NodeGRPCService(
            INodeRegistry nodeRegistry,
            IClusterConfiguration clusterConfig,
            ITestTriggerNotifier testTriggerNotifier,
            LPS.Domain.Common.Interfaces.ILogger logger, 
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ICustomGrpcClientFactory customGrpcClientFactory,
            CancellationTokenSource cts)
        {
            _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _clusterConfig = clusterConfig ?? throw new ArgumentNullException(nameof(clusterConfig));
            _testTriggerNotifier = testTriggerNotifier;
            _customGrpcClientFactory = customGrpcClientFactory;
            _cts = cts ?? throw new ArgumentNullException(nameof(_cts));
        }

        public override Task<RegisterNodeResponse> RegisterNode(LPS.Protos.Shared.NodeMetadata metadata, ServerCallContext context)
        {
            if (string.IsNullOrWhiteSpace(metadata.NodeName) || string.IsNullOrWhiteSpace(metadata.NodeIp))
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, "NodeName and NodeIp must be provided.", LPSLoggingLevel.Error);
                throw new RpcException(new Status(StatusCode.InvalidArgument, "NodeName and NodeIp must be provided."));
            }


            var nodeMetadata = metadata.FromProto(_clusterConfig);

            INode node = nodeMetadata.NodeType ==  NodeType.Master  ? new MasterNode(nodeMetadata, _clusterConfig, _nodeRegistry, _customGrpcClientFactory): new WorkerNode(nodeMetadata, _clusterConfig, _nodeRegistry, _customGrpcClientFactory);
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
                var node = _nodeRegistry.GetLocalNode();

                var response = new GetNodeStatusResponse
                {
                    Status = node.NodeStatus.ToGrpc()
                };
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Node Status Request Was Completed Successfully {node.Metadata.NodeName}", LPSLoggingLevel.Verbose, _cts.Token);

                return response;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to fetch node status.\n{ex.Message}\n{ex.InnerException}.", LPSLoggingLevel.Error, _cts.Token);
                throw new RpcException(new Status(StatusCode.Internal, "Failed to retrieve node status."));
            }
        }

        // Call the worker (by the master) to trigger the registered tests
        public override async Task<TriggerTestResponse> TriggerTest(TriggerTestRequest request, ServerCallContext context)
        {
            try
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Received TriggerTest request. Notifying CLI to start the test...", LPSLoggingLevel.Information, _cts.Token);

                _testTriggerNotifier.NotifyObservers();
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Trigger Test Request Was Completed Successfully", LPSLoggingLevel.Verbose, _cts.Token);
                return new TriggerTestResponse { Status = LPS.Protos.Shared.NodeStatus.Running };
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,  $"Failed to start test\n{ex.Message}\n{ex.InnerException}.", LPSLoggingLevel.Error, _cts.Token);
                return new TriggerTestResponse { Status = LPS.Protos.Shared.NodeStatus.Failed };
            }
        }

        // Worker updates the master with its status
        public override async Task<SetNodeStatusResponse> SetNodeStatus(SetNodeStatusRequest request, ServerCallContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.NodeName) || string.IsNullOrWhiteSpace(request.NodeIp))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "NodeName and NodeIp must be provided.", LPSLoggingLevel.Error, _cts.Token);
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "NodeName and NodeIp must be provided."));
                }
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Received status update from {request.NodeName}: {request.Status}", LPSLoggingLevel.Information, _cts.Token);

                // Update the node status in the registry (assuming INodeRegistry supports this)
                var node = _nodeRegistry.Query(n => n.Metadata.NodeName == request.NodeName && n.Metadata.NodeIP == request.NodeIp).Single();
                if (node == null)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Node {request.NodeName} not found in registry.", LPSLoggingLevel.Warning, _cts.Token);

                    return new SetNodeStatusResponse
                    {
                        Success = false,
                        Message = $"Node {request.NodeName} not found in registry."
                    };
                }

                await node.SetNodeStatus(request.Status.ToLocal());
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Node status was updated for {request.NodeIp}\\{request.NodeName}", LPSLoggingLevel.Verbose, _cts.Token);

                return new SetNodeStatusResponse
                {
                    Success = true,
                    Message = $"Status updated for {request.NodeName} to {request.Status}"
                };
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to process status report from {request.NodeIp}\\{request.NodeName}.\n{ex.Message}\n{ex.InnerException}.", LPSLoggingLevel.Error, _cts.Token);
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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,"Received CancelTest request. Cancelling local test...", LPSLoggingLevel.Verbose, _cts.Token);

                bool testCancelled = CancelLocalTest();
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Cancellation was signaled", LPSLoggingLevel.Verbose, _cts.Token);

                var status = testCancelled ? LPS.Protos.Shared.NodeStatus.Stopped : LPS.Protos.Shared.NodeStatus.Failed;
                return new CancelTestResponse
                {
                    Success = testCancelled,
                    Status = status
                };
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to cancel test.. {ex.Message}\n{ex.InnerException}", LPSLoggingLevel.Error, _cts.Token);

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
    }
}