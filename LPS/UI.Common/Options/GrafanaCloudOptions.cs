using System;

namespace LPS.UI.Common.Options
{
    /// <summary>
    /// Configuration options for Grafana Cloud integration.
    /// Metrics are pushed using Prometheus remote write protocol.
    /// </summary>
    public class GrafanaCloudOptions
    {
        /// <summary>
        /// Enable or disable Grafana Cloud integration.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Grafana Cloud Prometheus remote write endpoint.
        /// Example: "https://prometheus-us-central1.grafana.net/api/prom/push"
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Grafana Cloud username (usually your instance ID).
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Grafana Cloud API key or access token.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// How often to push metrics to Grafana Cloud (in seconds).
        /// Default: 5 seconds.
        /// </summary>
        public int PushIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Optional: Custom job name for metrics. Default: "lps".
        /// </summary>
        public string JobName { get; set; } = "lps";
    }
}
