using LPS.Domain.Common;
using LPS.Infrastructure.Watchdog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class LPSHttpClientOptions
    {
        public int? ClientTimeoutInSeconds { get;  set; }
        public int? PooledConnectionLifeTimeInSeconds { get; set; }
        public int? PooledConnectionIdleTimeoutInSeconds { get; set; }
        public int? MaxConnectionsPerServer { get; set; }
    }
}
