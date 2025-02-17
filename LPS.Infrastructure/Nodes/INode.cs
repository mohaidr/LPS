using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    public enum NodeType
    {
        Master,
        Slave
    }
    public interface INode
    {
        public static string NodeIP => GetLocalIPAddress();
        static string GetLocalIPAddress()
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                      .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?
                      .ToString() ?? "No IPv4 Address Found";
        }
        NodeType NodeType { get; }
        INodeMetadata Metadata { get; }
    }
}
