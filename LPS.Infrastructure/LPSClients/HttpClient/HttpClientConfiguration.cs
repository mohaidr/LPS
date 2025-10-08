using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// If your enum lives elsewhere, adjust this using accordingly:
using LPS.Infrastructure.LPSClients.HeaderServices;
using LPS.Infrastructure.Common.Interfaces; // HeaderValidationMode

namespace LPS.Infrastructure.LPSClients
{
    public class HttpClientConfiguration : ILPSHttpClientConfiguration<HttpRequest>
    {
        private HttpClientConfiguration()
        {
            PooledConnectionLifetime = TimeSpan.FromSeconds(1500);
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(300);
            MaxConnectionsPerServer = 1000;
            Timeout = TimeSpan.FromSeconds(240);
            // NEW defaults
            HeaderMode = HeaderValidationMode.RawPassthrough; // Strict | Lenient| RawPassthrough  (default) 
            AllowHostOverride = false;                  // keep Host managed by HttpClient unless explicitly allowed
        }

        /// <summary>
        /// NEW: ctor with header policy.
        /// </summary>
        public HttpClientConfiguration(
            TimeSpan pooledConnectionLifetime,
            TimeSpan pooledConnectionIdleTimeout,
            int maxConnectionsPerServer,
            TimeSpan timeout,
            HeaderValidationMode headerMode,
            bool allowHostOverride = false)
        {
            PooledConnectionLifetime = pooledConnectionLifetime;
            PooledConnectionIdleTimeout = pooledConnectionIdleTimeout;
            MaxConnectionsPerServer = maxConnectionsPerServer;
            Timeout = timeout;
            HeaderMode = headerMode;
            AllowHostOverride = allowHostOverride;
        }

        public static HttpClientConfiguration GetDefaultInstance()
        {
            return new HttpClientConfiguration();
        }

        public TimeSpan PooledConnectionLifetime { get; }
        public TimeSpan PooledConnectionIdleTimeout { get; }
        public int MaxConnectionsPerServer { get; }
        public TimeSpan Timeout { get; }

        /// <summary>
        /// NEW: Controls how request/content headers are validated/applied by the header service.
        /// </summary>
        public HeaderValidationMode HeaderMode { get; }

        /// <summary>
        /// NEW: When true (and if HeaderMode != Strict), allows overriding the Host header.
        /// Keep false unless you explicitly test virtual host/authority scenarios.
        /// </summary>
        public bool AllowHostOverride { get; }
    }
}
