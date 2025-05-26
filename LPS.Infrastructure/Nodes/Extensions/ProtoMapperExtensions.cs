using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using System.Collections.Generic;
using System.Linq;
using DiskInfo = LPS.Protos.Shared.DiskInfo;
using NetworkInfo = LPS.Protos.Shared.NetworkInfo;
using NodeMetadata = LPS.Protos.Shared.NodeMetadata;
using NodeType = LPS.Protos.Shared.NodeType;

namespace LPS.Infrastructure.Grpc
{
    public static class ProtoMapperExtensions
    {
        public static NodeMetadata ToProto(this INodeMetadata node)
        {
            return new NodeMetadata
            {
                NodeName = node.NodeName,
                NodeIp = node.NodeIP,
                NodeType = node.NodeType == LPS.Infrastructure.Nodes.NodeType.Master ? NodeType.Master : NodeType.Worker,
                Os = node.OS,
                Architecture = node.Architecture,
                Framework = node.Framework,
                Cpu = node.CPU,
                LogicalProcessors = node.LogicalProcessors,
                TotalRam = node.TotalRAM,
                Disks = { node.Disks.Select(d => d.ToProto()) },
                NetworkInterfaces = { node.NetworkInterfaces.Select(n => n.ToProto()) }
            };
        }

        public static DiskInfo ToProto(this IDiskInfo disk)
        {
            return new DiskInfo
            {
                Name = disk.Name,
                TotalSize = disk.TotalSize,
                FreeSpace = disk.FreeSpace
            };
        }

        public static NetworkInfo ToProto(this INetworkInfo network)
        {
            return new NetworkInfo
            {
                InterfaceName = network.InterfaceName,
                Type = network.Type,
                Status = network.Status,
                IpAddresses = { network.IpAddresses }
            };
        }
    }
}
