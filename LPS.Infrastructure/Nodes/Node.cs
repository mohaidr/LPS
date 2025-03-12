using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{

    public class Node: INode
    {
        public Node(INodeMetadata metadata) 
        { 
            Metadata = metadata;
            NodeStatus = NodeStatus.Waiting;
        }

        public INodeMetadata Metadata { get; }
        
        public NodeStatus NodeStatus { get; set; }
    }
}
