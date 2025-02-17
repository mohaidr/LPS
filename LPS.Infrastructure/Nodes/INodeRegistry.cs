using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    public interface INodeRegistry
    {
        public void RegisterNode(INode node);
        public void UnregisterNode(INode node);
        public IList<INode> FetchAllNodes(Func<INode, bool> predicate);
        public INode FetchMasterNode();
        public INode FetchLocalNode();
        public ICollection<INode> FetchNeighborNodes();
    }
}
