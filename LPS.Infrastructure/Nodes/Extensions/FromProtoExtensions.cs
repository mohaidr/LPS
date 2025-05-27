using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using System.Collections.Generic;
using System.Linq;
using DiskInfo = LPS.Protos.Shared.DiskInfo;
using NetworkInfo = LPS.Protos.Shared.NetworkInfo;
using NodeMetadata = LPS.Protos.Shared.NodeMetadata;

namespace LPS.Infrastructure.Grpc
{
    public static class FromProtoExtensions
    {
        public static LPS.Infrastructure.Nodes.NodeMetadata FromProto(this NodeMetadata proto, IClusterConfiguration clusterConfiguration)
        {
            return new LPS.Infrastructure.Nodes.NodeMetadata(
                clusterConfiguration,
                nodeName: proto.NodeName,
                nodeIP: proto.NodeIp,
                os: proto.Os,
                architecture: proto.Architecture,
                framework: proto.Framework,
                cpu: proto.Cpu,
                logicalProcessors: proto.LogicalProcessors,
                totalRam: proto.TotalRam,
                disks: proto.Disks.Select(d => d.FromProto()).Cast<IDiskInfo>().ToList(),
                networkInterfaces: proto.NetworkInterfaces.Select(n => n.FromProto()).Cast<INetworkInfo>().ToList()
            );
        }

        public static LPS.Infrastructure.Nodes.DiskInfo FromProto(this DiskInfo proto)
        {
            return new LPS.Infrastructure.Nodes.DiskInfo(
                name: proto.Name,
                totalSize: proto.TotalSize,
                freeSpace: proto.FreeSpace
            );
        }

        public static LPS.Infrastructure.Nodes.NetworkInfo FromProto(this NetworkInfo proto)
        {
            return new LPS.Infrastructure.Nodes.NetworkInfo(
                interfaceName: proto.InterfaceName,
                type: proto.Type,
                status: proto.Status,
                ipAddresses: proto.IpAddresses.ToList()
            );
        }
    }
}
