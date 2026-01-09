using System;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    /// <summary>
    /// Configuration options for InfluxDB integration.
    /// Metrics are uploaded to customer's InfluxDB instance.
    /// </summary>
    public class InfluxDBOptions
    {
        /// <summary>
        /// Enable or disable InfluxDB integration.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// InfluxDB server URL.
        /// Example: "https://us-east-1-1.aws.cloud2.influxdata.com" or "http://localhost:8086"
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Authentication token for InfluxDB API access.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Organization name in InfluxDB.
        /// </summary>
        public string Organization { get; set; } = string.Empty;

        /// <summary>
        /// Bucket name where metrics will be stored.
        /// </summary>
        public string Bucket { get; set; } = string.Empty;
    }
}
