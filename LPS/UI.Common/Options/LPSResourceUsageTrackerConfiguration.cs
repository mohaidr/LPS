using LPS.Domain.Common;
using LPS.Infrastructure.ResourceUsageTracker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class LPSResourceTrackerConfiguration
    {
        public double MaxMemoryMB { get; set; }
        public double CoolDownMemoryMB { get; set; }
        public double MaxCPUPercentage { get; set; }
        public double CoolDownCPUPercentage { get; set; }
        public int CoolDownRetryTimeInSeconds { get; set; }
        public SuspensionMode SuspensionMode { get; set; }
    }
}
