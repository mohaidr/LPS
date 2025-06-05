using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LPS.Protos.Shared;

namespace LPS.Infrastructure.Nodes
{
    public enum NodeType
    {
        Master,
        Worker
    }
    public enum NodeStatus
    {
        Created,
        Ready,
        Running,
        Failed,
        Stopped
    }
    public interface INode
    {
        public static string NodeIP => NodeUtility.GetLocalIPAddress();
        INodeMetadata Metadata { get; }
        NodeStatus NodeStatus { get; }
        public ValueTask<SetNodeStatusResponse> SetNodeStatus(NodeStatus nodeStatus);
        public bool IsActive();
        public bool IsInActive();
    }
}
