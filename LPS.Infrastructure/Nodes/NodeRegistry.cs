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

        public void RegisterNode(INode node)
        {
            if (!_nodes.Any(n=> n.Metadata == node.Metadata))
            {
                _nodes.Add(node);

                // Assign master node if it is the first node or explicitly marked
                if (_masterNode == null && node.Metadata.NodeType == NodeType.Master)
                {
                    _masterNode = node;
                }
            }
        }

        public void UnregisterNode(INode node)
        {
            if (_nodes.Remove(node) && node == _masterNode)
            {
                _masterNode = _nodes.FirstOrDefault(n => n.Metadata.NodeType == NodeType.Master);
            }
        }

        public IList<INode> FetchAllNodes(Func<INode, bool> predicate)
        {
            return _nodes.Where(predicate).ToList();
        }

        public INode FetchMasterNode()
        {
            return _masterNode ?? throw new InvalidOperationException("No master node registered.");
        }

        public INode FetchLocalNode()
        {
            _localNode ??= _nodes.FirstOrDefault(n => IsLocalNode(n));
            return _localNode ?? throw new InvalidOperationException("No local node found.");
        }

        public ICollection<INode> FetchNeighborNodes()
        {
            return _nodes.Where(n => !IsLocalNode(n)).ToList();
        }

        private bool IsLocalNode(INode node)
        {
            return node.Metadata.NetworkInterfaces
                .SelectMany(n => n.IpAddresses)
                .Contains(INode.GetLocalIPAddress());
        }
    }

}
