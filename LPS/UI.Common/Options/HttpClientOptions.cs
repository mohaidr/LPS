using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.HeaderServices;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Watchdog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class HttpClientOptions
    {
        public int? ClientTimeoutInSeconds { get; set; }
        public int? PooledConnectionLifeTimeInSeconds { get; set; }
        public int? PooledConnectionIdleTimeoutInSeconds { get; set; }
        public int? MaxConnectionsPerServer { get; set; }
        public HeaderValidationMode? HeaderValidationMode { get; set; }
        public bool? AllowHostOverride { get; set; }
        
        /// <summary>
        /// The response header name to read server processing time from.
        /// Examples: "Server-Timing", "X-Response-Time", "X-Runtime"
        /// </summary>
        public string? ServerTimeHeader { get; set; }
        
        /// <summary>
        /// Format of the server time header value.
        /// </summary>
        public ServerTimeFormat? ServerTimeFormat { get; set; }
    }
}
