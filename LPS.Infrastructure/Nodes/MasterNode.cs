using LPS.Infrastructure.Common.GRPCExtensions;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.GRPCClients;
using LPS.Protos.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    public class MasterNode : Node
    {
        public MasterNode(INodeMetadata metadata,
                          IClusterConfiguration clusterConfiguration,
                          INodeRegistry nodeRegistry,
                          ICustomGrpcClientFactory customGrpcClientFactory)
            : base(metadata, clusterConfiguration, nodeRegistry, customGrpcClientFactory) { }

        public override async ValueTask<SetNodeStatusResponse> SetNodeStatus(NodeStatus nodeStatus)
        {
            NodeStatus = nodeStatus;
            var localNode = _nodeRegistry.GetLocalNode();

            if (this.Metadata.NodeType == NodeType.Master && localNode.Metadata.NodeType == NodeType.Master)
            {
                foreach (var node in _nodeRegistry.GetNeighborNodes().Where(node => node.NodeStatus == NodeStatus.Running))
                {
                    var client = _customGrpcClientFactory.GetClient<GrpcNodeClient>(node.Metadata.NodeIP);
                    await client.SetNodeStatusAsync(new SetNodeStatusRequest() { NodeIp = this.Metadata.NodeIP, NodeName = this.Metadata.NodeName, Status = nodeStatus.ToGrpc() });
                }
            }
            return new SetNodeStatusResponse() { Success = true, Message = "Worker Node Status has been updated" };
        }
    }

}
