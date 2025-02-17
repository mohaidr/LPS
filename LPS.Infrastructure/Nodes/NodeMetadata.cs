using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    public class NodeMetadata : INodeMetadata
    {
        public string NodeName { get; }
        public string OS { get; }
        public string Architecture { get; }
        public string Framework { get; }
        public string CPU { get; }
        public int LogicalProcessors { get; }
        public string TotalRAM { get; }
        public List<IDiskInfo> Disks { get; }
        public List<INetworkInfo> NetworkInterfaces { get; }

        // Private constructor to prevent instantiation
        public NodeMetadata()
        {

            NodeName = Environment.MachineName;
            OS = RuntimeInformation.OSDescription;
            Architecture = RuntimeInformation.OSArchitecture.ToString();
            Framework = RuntimeInformation.FrameworkDescription;
            CPU = GetCpuInfo();
            LogicalProcessors = Environment.ProcessorCount;
            TotalRAM = GetMemoryInfo();
            Disks = GetDiskInfo().Cast<IDiskInfo>().ToList();
            NetworkInterfaces = GetNetworkInfo().Cast<INetworkInfo>().ToList();
        }

        private static string GetCpuInfo()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
            }
            else
            {
                try
                {
                    return File.ReadLines("/proc/cpuinfo").FirstOrDefault(line => line.StartsWith("model name"))?.Split(":")[1].Trim() ?? "Unknown CPU";
                }
                catch
                {
                    return "Unknown CPU";
                }
            }
        }

        private static string GetMemoryInfo()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"{GetTotalMemoryWindows()} MB";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    return File.ReadLines("/proc/meminfo").FirstOrDefault(line => line.StartsWith("MemTotal"))?.Split(":")[1].Trim() ?? "Unknown RAM";
                }
                catch
                {
                    return "Unknown RAM";
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    var output = ExecuteBashCommand("sysctl -n hw.memsize");
                    return $"{long.Parse(output.Trim()) / (1024 * 1024)} MB";
                }
                catch
                {
                    return "Unknown RAM";
                }
            }

            return "Unknown RAM";
        }

        private static long GetTotalMemoryWindows()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return Convert.ToInt64(obj["TotalPhysicalMemory"]) / (1024 * 1024);
                    }
                }
            }
            catch
            {
                return -1;
            }
            return -1;
        }

        private static List<DiskInfo> GetDiskInfo()
        {
            var diskList = new List<DiskInfo>();
            foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                diskList.Add(new DiskInfo
                {
                    Name = drive.Name,
                    TotalSize = $"{drive.TotalSize / (1024 * 1024 * 1024)} GB",
                    FreeSpace = $"{drive.AvailableFreeSpace / (1024 * 1024 * 1024)} GB"
                });
            }
            return diskList;
        }

        private static List<NetworkInfo> GetNetworkInfo()
        {
            var networkList = new List<NetworkInfo>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                var netInfo = new NetworkInfo
                {
                    InterfaceName = ni.Name,
                    Type = ni.NetworkInterfaceType.ToString(),
                    Status = ni.OperationalStatus.ToString(),
                    IPAddresses = ni.GetIPProperties().UnicastAddresses
                        .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork || ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        .Select(ip => ip.Address.ToString())
                        .ToList()
                };
                networkList.Add(netInfo);
            }
            return networkList;
        }

        private static string ExecuteBashCommand(string command)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                return process.StandardOutput.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public class DiskInfo : IDiskInfo
    {
        public string Name { get; set; }
        public string TotalSize { get; set; }
        public string FreeSpace { get; set; }
    }

    public class NetworkInfo : INetworkInfo
    {
        public string InterfaceName { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public List<string> IPAddresses { get; set; }
    }

}
