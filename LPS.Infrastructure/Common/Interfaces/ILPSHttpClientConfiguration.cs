using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.HeaderServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface ILPSHttpClientConfiguration<T> : IClientConfiguration<T> where T : IRequestEntity
    {
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
