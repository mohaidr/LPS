using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class DashboardConfigurationOptions
    {
        public bool? BuiltInDashboard { get; set; }
        public int? Port { get; set; }
        
        /// <summary>
        /// How often the dashboard UI should refresh (in seconds). Default is 5.
        /// </summary>
        public int? RefreshRate { get; set; }
        
        /// <summary>
        /// Window interval for windowed metrics aggregation (in seconds). Default is 5.
        /// This controls how often metrics are pushed via SignalR.
        /// </summary>
        public int? WindowIntervalSeconds { get; set; }
    }
}
