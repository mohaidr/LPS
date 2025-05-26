using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;
using LPS.Protos.Shared;
using LPS.Infrastructure.Common.GRPCExtensions;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.GRPCClients;

namespace LPS.Infrastructure.Nodes
{

    public class Node : INode
    {
        IClusterConfiguration _clusterConfiguration;
        INodeRegistry _nodeRegistry;
        ICustomGrpcClientFactory _customGrpcClientFactory;
        public Node(INodeMetadata metadata,
            IClusterConfiguration clusterConfiguration,
            INodeRegistry nodeRegistry,
            ICustomGrpcClientFactory customGrpcClientFactory)
        {
            Metadata = metadata;
            NodeStatus = NodeStatus.Pending;
            _nodeRegistry = nodeRegistry;
            _clusterConfiguration = clusterConfiguration;
            _customGrpcClientFactory = customGrpcClientFactory;
        }

        public INodeMetadata Metadata { get; }

        public NodeStatus NodeStatus { get; protected set; }

        public async ValueTask<SetNodeStatusResponse> SetNodeStatus(NodeStatus nodeStatus)
        {
            NodeStatus = nodeStatus;
            var localNode = _nodeRegistry.GetLocalNode();
            if (localNode.Metadata.NodeType == NodeType.Worker && this.Metadata.NodeType == NodeType.Worker)
            {
                // Create the gRPC Client
                var client = _customGrpcClientFactory.GetClient<GrpcNodeClient>(_clusterConfiguration.MasterNodeIP);
                var response = await client.SetNodeStatusAsync(new SetNodeStatusRequest() { NodeIp = this.Metadata.NodeIP, NodeName = this.Metadata.NodeName, Status = nodeStatus.ToGrpc() });
                return response;
            }
            else if(this.Metadata.NodeType == NodeType.Master && localNode.Metadata.NodeType == NodeType.Master)
            {
                foreach (var node in _nodeRegistry.GetNeighborNodes().Where(node=>node.NodeStatus == NodeStatus.Running))
                {
                    var client = _customGrpcClientFactory.GetClient<GrpcNodeClient>(node.Metadata.NodeIP);
                    await client.SetNodeStatusAsync(new SetNodeStatusRequest() { NodeIp = this.Metadata.NodeIP, NodeName = this.Metadata.NodeName, Status = nodeStatus.ToGrpc() });
                }
            }
            return new SetNodeStatusResponse() { Success = true, Message = "Master Node Status has been updated" };
        }
    }
}
