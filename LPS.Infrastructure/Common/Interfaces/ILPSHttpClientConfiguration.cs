using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.HeaderServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    /// <summary>
    /// Format of the server time header value.
    /// </summary>
    public enum ServerTimeFormat
    {
        /// <summary>
        /// Auto-detect format: tries Server-Timing syntax first, then numeric with optional 'ms' suffix.
        /// </summary>
        Auto,
        /// <summary>
        /// Plain numeric value in milliseconds.
        /// </summary>
        Milliseconds,
        /// <summary>
        /// Plain numeric value in seconds (will be converted to ms).
        /// </summary>
        Seconds,
        /// <summary>
        /// W3C Server-Timing format: parses 'dur=' values.
        /// </summary>
        ServerTiming
    }

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

        /// <summary>
        /// The response header name to read server processing time from.
        /// Examples: "Server-Timing", "X-Response-Time", "X-Runtime"
        /// Null or empty means disabled.
        /// </summary>
        public string? ServerTimeHeader { get; }

        /// <summary>
        /// Format of the server time header value.
        /// </summary>
        public ServerTimeFormat ServerTimeFormat { get; }
    }
}
