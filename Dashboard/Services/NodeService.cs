    using LPS.Infrastructure.Nodes;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Apis.Controllers;
    using static Apis.NodeService; // This is where the gRPC definition is

    namespace Apis.Services
    {
        public class NodeService : NodeServiceBase
        {
            private readonly INodeRegistry _nodeRegistry;
            private readonly ILogger<NodeService> _logger;
            IClusterConfiguration _clusterConfig;
            public NodeService(INodeRegistry nodeRegistry, IClusterConfiguration clusterConfig, ILogger<NodeService> logger)
            {
                _nodeRegistry = nodeRegistry;
                _logger = logger;
                _clusterConfig = clusterConfig;
            }

            public override Task<RegisterNodeResponse> RegisterNode(NodeMetadata request, ServerCallContext context)
            {
                var disks = request.Disks.Select(d => new LPS.Infrastructure.Nodes.DiskInfo(d.Name, d.TotalSize, d.FreeSpace)).ToList<IDiskInfo>();
                var networkInterfaces = request.NetworkInterfaces.Select(n => new LPS.Infrastructure.Nodes.NetworkInfo(n.InterfaceName, n.Type, n.Status, n.IpAddresses.ToList())).ToList<INetworkInfo>();

                var nodeMetadata = new LPS.Infrastructure.Nodes.NodeMetadata(
                    _clusterConfig,
                    request.NodeName,
                    request.Os,
                    request.Architecture,
                    request.Framework,
                    request.Cpu,
                    request.LogicalProcessors,
                    request.TotalRam,
                    disks,
                    networkInterfaces
                );

                var node = new Node(nodeMetadata); // Assuming `Node` implements `INode`
                _nodeRegistry.RegisterNode(node);

                _logger.LogInformation($"Registered Node: {node.Metadata.NodeName} as {node.Metadata.NodeType}");

                return Task.FromResult(new RegisterNodeResponse
                {
                    Message = $"Node {request.NodeName} registered successfully as {request.NodeType}"
                });
            }
        }
    }
