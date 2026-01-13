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
        /// How often metrics are pushed to the dashboard via SignalR (in seconds). Default is 3.
        /// This controls both windowed and cumulative metrics push intervals.
        /// </summary>
        public int? RefreshRate { get; set; }
    }
}
