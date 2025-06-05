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
using LPS.Infrastructure.Nodes;
using Node = LPS.Infrastructure.Nodes.Node;
using NodeType = LPS.Infrastructure.Nodes.NodeType;
using NodeStatus = LPS.Infrastructure.Nodes.NodeStatus;

namespace LPS.Infrastructure.Nodes
{

    public abstract class Node : INode
    {
        protected IClusterConfiguration _clusterConfiguration;
        protected INodeRegistry _nodeRegistry;
        protected ICustomGrpcClientFactory _customGrpcClientFactory;
        public Node(INodeMetadata metadata,
            IClusterConfiguration clusterConfiguration,
            INodeRegistry nodeRegistry,
            ICustomGrpcClientFactory customGrpcClientFactory)
        {
            Metadata = metadata;
            NodeStatus = NodeStatus.Created;
            _nodeRegistry = nodeRegistry;
            _clusterConfiguration = clusterConfiguration;
            _customGrpcClientFactory = customGrpcClientFactory;
        }

        public INodeMetadata Metadata { get; }

        public NodeStatus NodeStatus { get; protected set; }

        public abstract ValueTask<SetNodeStatusResponse> SetNodeStatus(NodeStatus nodeStatus);

        public bool IsActive() => NodeStatus == NodeStatus.Running || NodeStatus == NodeStatus.Ready || NodeStatus == NodeStatus.Created;

        public bool IsInActive() => NodeStatus == NodeStatus.Failed || NodeStatus == NodeStatus.Stopped;


    }

}


