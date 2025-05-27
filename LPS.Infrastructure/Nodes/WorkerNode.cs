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
    public class WorkerNode : Node
    {
        public WorkerNode(INodeMetadata metadata,
                          IClusterConfiguration clusterConfiguration,
                          INodeRegistry nodeRegistry,
                          ICustomGrpcClientFactory customGrpcClientFactory)
            : base(metadata, clusterConfiguration, nodeRegistry, customGrpcClientFactory) { }

        public override async ValueTask<SetNodeStatusResponse> SetNodeStatus(NodeStatus nodeStatus)
        {
            NodeStatus = nodeStatus;
            var localNode = _nodeRegistry.GetLocalNode();
            if (localNode.Metadata.NodeType == NodeType.Worker && this.Metadata.NodeType == NodeType.Worker && (_nodeRegistry.GetMasterNode().NodeStatus == NodeStatus.Running || _nodeRegistry.GetMasterNode().NodeStatus == NodeStatus.Ready))
            {
                // Create the gRPC Client
                var client = _customGrpcClientFactory.GetClient<GrpcNodeClient>(_clusterConfiguration.MasterNodeIP);
                var response = await client.SetNodeStatusAsync(new SetNodeStatusRequest() { NodeIp = this.Metadata.NodeIP, NodeName = this.Metadata.NodeName, Status = nodeStatus.ToGrpc() });
                return response;
            }
            return new SetNodeStatusResponse() { Success = true, Message = "Master Node Status has been updated" };
        }
    }

}
