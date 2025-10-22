using LPS.Infrastructure.Common.GRPCExtensions;
using LPS.Infrastructure.Grpc;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Protos.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
                   var result= client.RegisterNode(node.Metadata.ToProto()); // register on the master node
                }
                if (_nodeMetadata.NodeType == NodeType.Master && node.Metadata.NodeType == NodeType.Worker)// master regestering itself to worker
                {
                    var client = _customGrpcClientFactory.GetClient<GrpcNodeClient>(node.Metadata.NodeIP);
                    // Call the gRPC Service
                    client.RegisterNode(_nodeMetadata.ToProto()); // register the master on the local node
                    client.SetNodeStatus(new SetNodeStatusRequest { NodeIp = _nodeMetadata.NodeIP, NodeName = _nodeMetadata.NodeName, Status =  _masterNode?.NodeStatus.ToGrpc()?? NodeStatus.Created.ToGrpc() });
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

        public bool TryGetMasterNode([NotNullWhen(true)] out INode? masterNode)
        {
            masterNode = _masterNode ?? _nodes.FirstOrDefault(n => n.Metadata.NodeType == NodeType.Master);
            if (masterNode is not null)
            {
                _masterNode = masterNode; // cache
                return true;
            }
            return false;
        }

        public bool TryGetLocalNode([NotNullWhen(true)] out INode? localNode)
        {
            localNode = _localNode ?? _nodes.FirstOrDefault(IsLocalNode);
            if (localNode is not null)
            {
                _localNode = localNode; // cache
                return true;
            }
            return false;
        }

        public IEnumerable<INode> GetNeighborNodes()
        {
            return _nodes.Where(n => !IsLocalNode(n));
        }

        public IEnumerable<INode> GetActiveNodes()
        {
            return _nodes.Where(node => node.IsActive());
        }
        public IEnumerable<INode> GetInActiveNodes()
        {
            return _nodes.Where(node => node.IsInActive());
        }
        private bool IsLocalNode(INode node)
        {
            return node.Metadata.NodeIP == INode.NodeIP;
        }
    }

}
