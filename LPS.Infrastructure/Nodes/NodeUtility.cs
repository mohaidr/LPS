using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    internal class NodeUtility
    {
        public static string GetLocalIPAddress()
        {

            {
                try
                {
                    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                    {
                        // Use a dummy public IP and port. No data will actually be sent.
                        socket.Connect("8.8.8.8", 65530);
                        var endPoint = socket.LocalEndPoint as IPEndPoint;
                        return endPoint?.Address.ToString() ?? throw new InvalidOperationException("No IPv4 address could be determined for this host.");
                    }
                }
                catch
                {
                    return Dns.GetHostAddresses(Dns.GetHostName())
                        .FirstOrDefault(ip =>
                            ip.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(ip))?.ToString() ?? throw new InvalidOperationException("No IPv4 address could be determined for this host.");
                }
            }
        }
    }
}
