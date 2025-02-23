using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace LPS.Infrastructure.Nodes
{
    public class NodeMetadata : INodeMetadata
    {
        private readonly IClusterConfiguration _clusterConfiguration;

        public string NodeName { get; }
        public NodeType NodeType => INode.NodeIP == _clusterConfiguration.MasterNodeIP ? NodeType.Master : NodeType.Worker;
        public string OS { get; }
        public string Architecture { get; }
        public string Framework { get; }
        public string CPU { get; }
        public int LogicalProcessors { get; }
        public string TotalRAM { get; }
        public List<IDiskInfo> Disks { get; }
        public List<INetworkInfo> NetworkInterfaces { get; }

        public NodeMetadata(IClusterConfiguration clusterConfiguration)
        {
            _clusterConfiguration = clusterConfiguration;
            NodeName = Environment.MachineName;
            OS = RuntimeInformation.OSDescription;
            Architecture = RuntimeInformation.OSArchitecture.ToString();
            Framework = RuntimeInformation.FrameworkDescription;
            CPU = GetCpuInfo();
            LogicalProcessors = Environment.ProcessorCount;
            TotalRAM = GetMemoryInfo();
            Disks = new List<IDiskInfo>();
            NetworkInterfaces = new List<INetworkInfo>();
        }

        public NodeMetadata(
            IClusterConfiguration clusterConfiguration,
            string nodeName,
            string os,
            string architecture,
            string framework,
            string cpu,
            int logicalProcessors,
            string totalRam,
            List<IDiskInfo> disks,
            List<INetworkInfo> networkInterfaces)
        {
            _clusterConfiguration = clusterConfiguration;
            NodeName = nodeName;
            OS = os;
            Architecture = architecture;
            Framework = framework;
            CPU = cpu;
            LogicalProcessors = logicalProcessors;
            TotalRAM = totalRam;
            Disks = disks ?? throw new ArgumentNullException(nameof(disks));
            NetworkInterfaces = networkInterfaces ?? throw new ArgumentNullException(nameof(networkInterfaces));
        }

        private static string GetCpuInfo()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU" :
                File.ReadLines("/proc/cpuinfo").FirstOrDefault(line => line.StartsWith("model name"))?.Split(":")[1].Trim() ?? "Unknown CPU";
        }

        private static string GetMemoryInfo()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "Unknown RAM";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return File.ReadLines("/proc/meminfo").FirstOrDefault(line => line.StartsWith("MemTotal"))?.Split(":")[1].Trim() ?? "Unknown RAM";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var output = "Unknown RAM";
                return output;
            }
            return "Unknown RAM";
        }
    }

    public class DiskInfo : IDiskInfo
    {
        public string Name { get; }
        public string TotalSize { get; }
        public string FreeSpace { get; }

        public DiskInfo(string name, string totalSize, string freeSpace)
        {
            Name = name;
            TotalSize = totalSize;
            FreeSpace = freeSpace;
        }
    }

    public class NetworkInfo : INetworkInfo
    {
        public string InterfaceName { get; }
        public string Type { get; }
        public string Status { get; }
        public List<string> IpAddresses { get; }

        public NetworkInfo(string interfaceName, string type, string status, List<string> ipAddresses)
        {
            InterfaceName = interfaceName;
            Type = type;
            Status = status;
            IpAddresses = ipAddresses ?? new List<string>();
        }
    }
}
