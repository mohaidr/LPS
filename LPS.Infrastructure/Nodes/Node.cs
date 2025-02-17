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
        public Node(INodeMetadata metadata, IClusterConfiguration clusterConfiguration) 
        { 
            if(INode.NodeIP == clusterConfiguration.MasterNodeIP)
                NodeType = NodeType.Master;
            else NodeType = NodeType.Slave;

            Metadata = metadata;
        }

        public NodeType NodeType { get; }
        public INodeMetadata Metadata { get; }
    }
}
