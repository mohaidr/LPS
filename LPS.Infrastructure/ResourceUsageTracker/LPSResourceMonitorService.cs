using LPS.Domain.Common;
using System;
using System.Diagnostics;
using System.Threading;

namespace LPS.Infrastructure.ResourceUsageTracker
{
    internal class LPSResourceMonitorService
    {
        private Timer _timer;
        private double _memoryUsageMB;
        private double _cpuTime;

        public LPSResourceMonitorService()
        {
            _timer = new Timer(UpdateResourceUsage, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        }

        public double MemoryUsageMB { get { return _memoryUsageMB; } }
        public double CPUPercentage { get { return _cpuTime; } }
        private void UpdateResourceUsage(object state)
        {
            Process process = Process.GetCurrentProcess();
            _memoryUsageMB = Math.Round(process.PrivateMemorySize64 / 1048576.0, 2);
            TimeSpan cpuTime = process.TotalProcessorTime;
            TimeSpan elapsedTime = DateTime.Now - process.StartTime;
            _cpuTime = 100.0 * cpuTime.TotalMilliseconds / elapsedTime.TotalMilliseconds / Environment.ProcessorCount;
        }
    }
}
