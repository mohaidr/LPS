using LPS.Domain.Common;
using LPS.Infrastructure.Watchdog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class LPSWatchDogOptions
    {
        public double MaxMemoryMB { get; set; }
        public double CoolDownMemoryMB { get; set; }
        public double MaxCPUPercentage { get; set; }
        public double CoolDownCPUPercentage { get; set; }
        public int CoolDownRetryTimeInSeconds { get; set; }
        public int MaxConnectionsCountPerHostName { get; set; }
        public int CoolDownConnectionsCountPerHostName { get; set; }
        public SuspensionMode SuspensionMode { get; set; }
    }
}
