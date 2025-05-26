using LPS.Infrastructure.Common.GRPCExtensions;
using LPS.Infrastructure.Grpc;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Protos.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    public class NodeRegistry : INodeRegistry
    {
        private readonly List<INode> _nodes = new();
        private INode? _masterNode;
        private INode? _localNode;
        readonly IClusterConfiguration _clusterConfiguration;
        ICustomGrpcClientFactory _customGrpcClientFactory;
        INodeMetadata _nodeMetadata;
        public NodeRegistry(ICustomGrpcClientFactory customGrpcClientFactory,
        IClusterConfiguration clusterConfiguration, INodeMetadata nodeMetadata) 
        {
            _clusterConfiguration = clusterConfiguration;
            _customGrpcClientFactory = customGrpcClientFactory;
            _nodeMetadata = nodeMetadata;
        }
        public void RegisterNode(INode node)
        {
            // Do not compare as records becuase the recode contains reference based comparison types
            if (!_nodes.Any(n=> n.Metadata.NodeIP == node.Metadata.NodeIP && n.Metadata.NodeName == node.Metadata.NodeName))
            {
                _nodes.Add(node);
                // Assign master node if it is the first node or explicitly marked
                if (_masterNode == null && node.Metadata.NodeType == NodeType.Master)
                {
                    _masterNode = node;
                }
                if (_nodeMetadata.NodeType == NodeType.Worker && _nodeMetadata.NodeIP == node.Metadata.NodeIP) // worker registering itself to master
                {
                    var client = _customGrpcClientFactory.GetClient<GrpcNodeClient>(_clusterConfiguration.MasterNodeIP);
                    // Call the gRPC Service
                    client.RegisterNode(node.Metadata.ToProto()); // register on the master node
                }
                else
                if (_nodeMetadata.NodeType == NodeType.Master && node.Metadata.NodeType == NodeType.Worker)// master regestering itself to worker
                {
                    var client = _customGrpcClientFactory.GetClient<GrpcNodeClient>(node.Metadata.NodeIP);
                    // Call the gRPC Service
                    client.RegisterNode(_nodeMetadata.ToProto()); // register the master on the local node
                    client.SetNodeStatus(new SetNodeStatusRequest { NodeIp = _nodeMetadata.NodeIP, NodeName = _nodeMetadata.NodeName, Status =  _masterNode?.NodeStatus.ToGrpc()?? NodeStatus.Pending.ToGrpc() });
                }
            }
        }

        public void UnregisterNode(INode node)
        {
            if (_nodes.Remove(node) && node.Metadata.NodeType == NodeType.Master)
            {
                _masterNode = _nodes.FirstOrDefault(n => n.Metadata.NodeType == NodeType.Master);
            }
        }

        public IEnumerable<INode> Query(Func<INode, bool> predicate)
        {
            return _nodes.Where(predicate);
        }

        public INode GetMasterNode()
        {
            return _masterNode ?? throw new InvalidOperationException("No master node registered.");
        }

        public INode GetLocalNode()
        {
            _localNode ??= _nodes.FirstOrDefault(n => IsLocalNode(n));
            return _localNode ?? throw new InvalidOperationException("No local node found.");
        }

        public IEnumerable<INode> GetNeighborNodes()
        {
            return _nodes.Where(n => !IsLocalNode(n));
        }

        private bool IsLocalNode(INode node)
        {
            return node.Metadata.NodeIP == INode.GetLocalIPAddress();
        }
    }

}
