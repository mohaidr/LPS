using LPS.Infrastructure.Nodes;
using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Apis.Controllers;
using static LPS.Protos.Shared.NodeService;
using LPS.Protos.Shared;

namespace Apis.Services
{
    public class NodeGRPCService : NodeServiceBase
    {
        private readonly INodeRegistry _nodeRegistry;
        private readonly ILogger<NodeGRPCService> _logger;
        IClusterConfiguration _clusterConfig;
        public NodeGRPCService(INodeRegistry nodeRegistry, IClusterConfiguration clusterConfig, ILogger<NodeGRPCService> logger)
        {
            _nodeRegistry = nodeRegistry;
            _logger = logger;
            _clusterConfig = clusterConfig;
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

            var node = new Node(nodeMetadata);
            _nodeRegistry.RegisterNode(node);

            _logger.LogInformation($"Registered Node: {node.Metadata.NodeName} as {node.Metadata.NodeType}");

            return Task.FromResult(new RegisterNodeResponse
            {
                Message = $"Node {metadata.NodeName} registered successfully as {metadata.NodeType}"
            });
        }
    }
}
