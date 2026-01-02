using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    /// <summary>
    /// Service that pushes metrics to Grafana Cloud using Prometheus remote write protocol.
    /// </summary>
    public interface IGrafanaCloudPusher : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Start the background push loop.
        /// </summary>
        Task StartAsync(CancellationToken token);

        /// <summary>
        /// Stop the background push loop.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Force an immediate push of all current metrics (bypasses change detection).
        /// Call this before disposing to ensure all data is flushed.
        /// </summary>
        Task FlushAsync(CancellationToken token = default);

        /// <summary>
        /// Whether the pusher is currently running.
        /// </summary>
        bool IsRunning { get; }
    }

    /// <summary>
    /// Configuration for Grafana Cloud pusher (passed from options).
    /// </summary>
    public class GrafanaCloudConfig
    {
        public bool Enabled { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public int PushIntervalSeconds { get; set; } = 5;
        public string JobName { get; set; } = "lps";
    }

    public sealed class GrafanaCloudPusher : IGrafanaCloudPusher
    {
        private readonly GrafanaCloudConfig _config;
        private readonly IMetricDataStore _dataStore;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _op;
        private readonly HttpClient _httpClient;

        // Change detection: track last pushed completed count per iteration
        // We track (SuccessfulRequestCount + FailedRequestsCount) because:
        // - RequestsCount only increases when NEW requests start
        // - Completed count also increases when in-flight requests finish
        // This ensures we push updates even when no new requests start but existing ones complete
        private readonly Dictionary<Guid, long> _lastPushedCompletedCounts = new();

        private Timer? _timer;
        private bool _isRunning;
        private bool _disposed;

        public bool IsRunning => _isRunning;

        public GrafanaCloudPusher(
            GrafanaCloudConfig config,
            IMetricDataStore dataStore,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _op = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));

            _httpClient = new HttpClient();

            // Set up Basic Auth for Grafana Cloud
            if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.ApiKey))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{_config.Username}:{_config.ApiKey}");
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }
        }

        public Task StartAsync(CancellationToken token)
        {
            if (!_config.Enabled)
            {
                _logger.Log(_op.OperationId, "Grafana Cloud integration is disabled", LPSLoggingLevel.Verbose);
                return Task.CompletedTask;
            }

            if (string.IsNullOrEmpty(_config.Endpoint))
            {
                _logger.Log(_op.OperationId, "Grafana Cloud endpoint is not configured", LPSLoggingLevel.Warning);
                return Task.CompletedTask;
            }

            _isRunning = true;
            var intervalMs = _config.PushIntervalSeconds * 1000;

            _timer = new Timer(
                async _ => await PushMetricsAsync(token),
                null,
                intervalMs, // first push after interval
                intervalMs);

            _logger.Log(_op.OperationId,
                $"Grafana Cloud pusher started. Pushing every {_config.PushIntervalSeconds}s to {_config.Endpoint}",
                LPSLoggingLevel.Information);

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _isRunning = false;
            _timer?.Dispose();
            _timer = null;

            _logger.Log(_op.OperationId, "Grafana Cloud pusher stopped", LPSLoggingLevel.Information);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Force an immediate push of all current metrics, bypassing change detection.
        /// This ensures all final metrics are sent before the application exits.
        /// </summary>
        public async Task FlushAsync(CancellationToken token = default)
        {
            if (!_config.Enabled || string.IsNullOrEmpty(_config.Endpoint))
                return;

            try
            {
                var graphiteLines = BuildGraphiteFormatForced();

                if (string.IsNullOrEmpty(graphiteLines))
                    return;

                var graphiteEndpoint = _config.Endpoint
                    .Replace("prometheus-", "graphite-")
                    .Replace("/api/prom/push", "/graphite/metrics");

                var content = new StringContent(graphiteLines, Encoding.UTF8);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var response = await _httpClient.PostAsync(graphiteEndpoint, content, token);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(token);
                    _logger.Log(_op.OperationId,
                        $"Final flush to Grafana Cloud failed: {response.StatusCode} - {body}",
                        LPSLoggingLevel.Warning);
                }
                else
                {
                    _logger.Log(_op.OperationId, "Final metrics flushed to Grafana Cloud successfully", LPSLoggingLevel.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.Log(_op.OperationId,
                    $"Error during final flush to Grafana Cloud: {ex.Message}",
                    LPSLoggingLevel.Error);
            }
        }

        private async Task PushMetricsAsync(CancellationToken token)
        {
            if (!_isRunning || token.IsCancellationRequested)
                return;

            try
            {
                var graphiteLines = BuildGraphiteFormat();

                if (string.IsNullOrEmpty(graphiteLines))
                    return; // No metrics to push

                // Use Grafana Cloud's Graphite endpoint which accepts plain text
                // Convert prometheus endpoint to graphite endpoint
                // From: https://prometheus-prod-53-prod-me-central-1.grafana.net/api/prom/push
                // To:   https://graphite-prod-53-prod-me-central-1.grafana.net/graphite/metrics
                var graphiteEndpoint = _config.Endpoint
                    .Replace("prometheus-", "graphite-")
                    .Replace("/api/prom/push", "/graphite/metrics");

                var content = new StringContent(graphiteLines, Encoding.UTF8);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var response = await _httpClient.PostAsync(graphiteEndpoint, content, token);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(token);
                    await _logger.LogAsync(_op.OperationId,
                        $"Failed to push metrics to Grafana Cloud: {response.StatusCode} - {body}",
                        LPSLoggingLevel.Warning, token);
                }
                else
                {
                    _logger.Log(_op.OperationId, "Metrics pushed to Grafana Cloud successfully", LPSLoggingLevel.Verbose);
                }
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId,
                    $"Error pushing metrics to Grafana Cloud: {ex.Message}",
                    LPSLoggingLevel.Error, token);
            }
        }

        /// <summary>
        /// Checks if any iteration has new data since last push.
        /// Returns true if there's new data to push.
        /// </summary>
        private bool HasNewData()
        {
            foreach (var iteration in _dataStore.Iterations)
            {
                if (!_dataStore.TryGetLatest(iteration.Id, out var snapshots))
                    continue;

                foreach (var snapshot in snapshots)
                {
                    if (snapshot is ThroughputMetricSnapshot throughput)
                    {
                        // Track completed requests (success + failed)
                        // This captures both:
                        // 1. New requests starting (eventually complete)
                        // 2. In-flight requests finishing (even if no new ones start)
                        var completedCount = throughput.SuccessfulRequestCount + throughput.FailedRequestsCount;
                        
                        if (!_lastPushedCompletedCounts.TryGetValue(iteration.Id, out var lastCount) ||
                            lastCount != completedCount)
                        {
                            return true; // New data found
                        }
                    }
                }
            }
            return false; // No changes
        }

        /// <summary>
        /// Updates the tracking dictionary with current completed request counts.
        /// </summary>
        private void UpdateLastPushedCounts()
        {
            foreach (var iteration in _dataStore.Iterations)
            {
                if (!_dataStore.TryGetLatest(iteration.Id, out var snapshots))
                    continue;

                foreach (var snapshot in snapshots)
                {
                    if (snapshot is ThroughputMetricSnapshot throughput)
                    {
                        var completedCount = throughput.SuccessfulRequestCount + throughput.FailedRequestsCount;
                        _lastPushedCompletedCounts[iteration.Id] = completedCount;
                    }
                }
            }
        }

        /// <summary>
        /// Builds Graphite JSON format from current metrics.
        /// Format: [{"name": "metric.name", "value": 123, "interval": 10, "time": 1234567890, "tags": ["tag=value"]}]
        /// </summary>
        private string BuildGraphiteFormat()
        {
            // Check if there's new data first
            if (!HasNewData())
            {
                return string.Empty; // Skip - no changes since last push
            }

            return BuildGraphiteFormatInternal(updateTracking: true);
        }

        /// <summary>
        /// Builds Graphite JSON format, bypassing change detection (for final flush).
        /// </summary>
        private string BuildGraphiteFormatForced()
        {
            return BuildGraphiteFormatInternal(updateTracking: false);
        }

        /// <summary>
        /// Internal method that builds Graphite JSON from current metrics.
        /// </summary>
        private string BuildGraphiteFormatInternal(bool updateTracking)
        {
            var metrics = new List<object>();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var job = _config.JobName;

            foreach (var iteration in _dataStore.Iterations)
            {
                if (!_dataStore.TryGetLatest(iteration.Id, out var snapshots))
                    continue;

                foreach (var snapshot in snapshots)
                {
                    AppendGraphiteMetrics(metrics, snapshot, job, timestamp);
                }
            }

            // Update tracking after building metrics (only for regular pushes, not flush)
            if (updateTracking)
            {
                UpdateLastPushedCounts();
            }

            if (metrics.Count == 0)
                return string.Empty;

            return System.Text.Json.JsonSerializer.Serialize(metrics);
        }

        private void AppendGraphiteMetrics(List<object> metrics, HttpMetricSnapshot snapshot, string job, long timestamp)
        {
            var roundName = SanitizeTag(snapshot.RoundName ?? "unknown");
            var iterationName = SanitizeTag(snapshot.IterationName ?? "unknown");
            var method = SanitizeTag(snapshot.HttpMethod ?? "unknown");
            var url = SanitizeUrl(snapshot.URL ?? "unknown");
            var runId = SanitizeTag(_op.OperationId.ToString().Substring(0, 8)); // First 8 chars of correlation ID
            var tags = new[] { $"job={job}", $"run={runId}", $"round={roundName}", $"iteration={iterationName}", $"method={method}", $"url={url}" };

            switch (snapshot)
            {
                case ThroughputMetricSnapshot t:
                    // Request counts
                    metrics.Add(new { name = "lps.requests.total", value = t.RequestsCount, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    metrics.Add(new { name = "lps.requests.active", value = t.ActiveRequestsCount, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    metrics.Add(new { name = "lps.requests.success", value = t.SuccessfulRequestCount, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    metrics.Add(new { name = "lps.requests.failed", value = t.FailedRequestsCount, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    metrics.Add(new { name = "lps.error_rate", value = t.ErrorRate, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    // Request rates
                    metrics.Add(new { name = "lps.requests_per_second", value = t.RequestsRate.Value, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    metrics.Add(new { name = "lps.requests_per_cooldown", value = t.RequestsRatePerCoolDownPeriod.Value, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    break;

                case DurationMetricSnapshot d:
                    // All 7 timing metrics with full aggregations (min, avg, max, sum, p50, p90, p95, p99)
                    // Use original metric names compatible with modern dashboard
                    AppendDurationGraphite(metrics, "lps.total_time", d.TotalTimeMetrics, tags, timestamp);
                    AppendDurationGraphite(metrics, "lps.tcp_handshake", d.TCPHandshakeTimeMetrics, tags, timestamp);
                    AppendDurationGraphite(metrics, "lps.tls_handshake", d.SSLHandshakeTimeMetrics, tags, timestamp);
                    AppendDurationGraphite(metrics, "lps.ttfb", d.TimeToFirstByteMetrics, tags, timestamp);
                    AppendDurationGraphite(metrics, "lps.waiting", d.WaitingTimeMetrics, tags, timestamp);
                    AppendDurationGraphite(metrics, "lps.sending", d.SendingTimeMetrics, tags, timestamp);
                    AppendDurationGraphite(metrics, "lps.receiving", d.ReceivingTimeMetrics, tags, timestamp);
                    break;

                case ResponseCodeMetricSnapshot r:
                    // Response status code breakdown
                    foreach (var summary in r.ResponseSummaries)
                    {
                        var statusCode = (int)summary.HttpStatusCode;
                        var statusTags = new[] { $"job={job}", $"run={runId}", $"round={roundName}", $"iteration={iterationName}", $"method={method}", $"url={url}", $"status={statusCode}" };
                        metrics.Add(new { name = "lps.response.status_count", value = summary.Count, interval = _config.PushIntervalSeconds, time = timestamp, tags = statusTags });
                    }
                    break;

                case DataTransmissionMetricSnapshot dt:
                    // Data transfer totals
                    metrics.Add(new { name = "lps.data.sent_bytes", value = dt.DataSent, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    metrics.Add(new { name = "lps.data.received_bytes", value = dt.DataReceived, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    
                    // Per-request averages
                    metrics.Add(new { name = "lps.data.avg_sent_per_request", value = dt.AverageDataSent, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    metrics.Add(new { name = "lps.data.avg_received_per_request", value = dt.AverageDataReceived, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    
                    // Throughput rates (bytes per second)
                    metrics.Add(new { name = "lps.data.upstream_bps", value = dt.UpstreamThroughputBps, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    metrics.Add(new { name = "lps.data.downstream_bps", value = dt.DownstreamThroughputBps, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    metrics.Add(new { name = "lps.data.throughput_bps", value = dt.ThroughputBps, interval = _config.PushIntervalSeconds, time = timestamp, tags });
                    break;
            }
        }

        private void AppendDurationGraphite(List<object> metrics, string name, DurationMetricSnapshot.MetricTime m, string[] tags, long ts)
        {
            if (m == null) return;
            
            // All aggregations: min, avg, max, sum, p50, p90, p95, p99
            // Push even if values are 0 - they're valid data points
            metrics.Add(new { name = $"{name}.min", value = m.Min, interval = _config.PushIntervalSeconds, time = ts, tags });
            metrics.Add(new { name = $"{name}.avg", value = m.Average, interval = _config.PushIntervalSeconds, time = ts, tags });
            metrics.Add(new { name = $"{name}.max", value = m.Max, interval = _config.PushIntervalSeconds, time = ts, tags });
            metrics.Add(new { name = $"{name}.sum", value = m.Sum, interval = _config.PushIntervalSeconds, time = ts, tags });
            metrics.Add(new { name = $"{name}.p50", value = m.P50, interval = _config.PushIntervalSeconds, time = ts, tags });
            metrics.Add(new { name = $"{name}.p90", value = m.P90, interval = _config.PushIntervalSeconds, time = ts, tags });
            metrics.Add(new { name = $"{name}.p95", value = m.P95, interval = _config.PushIntervalSeconds, time = ts, tags });
            metrics.Add(new { name = $"{name}.p99", value = m.P99, interval = _config.PushIntervalSeconds, time = ts, tags });
        }

        private static string SanitizeTag(string value)
        {
            // Graphite tags: replace spaces and special chars
            return value
                .Replace(" ", "_")
                .Replace(";", "_")
                .Replace("=", "_")
                .Replace(".", "_");
        }

        private static string SanitizeUrl(string url)
        {
            // Extract just the path from URL for cleaner tags
            // e.g., "https://api.example.com/users/123?q=test" -> "/users/123"
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var path = uri.AbsolutePath;
                    // Truncate very long paths and sanitize
                    if (path.Length > 50)
                        path = path.Substring(0, 50);
                    return SanitizeTag(path);
                }
                // Relative URL - just sanitize directly
                var sanitized = url.Length > 50 ? url.Substring(0, 50) : url;
                return SanitizeTag(sanitized);
            }
            catch
            {
                return "unknown";
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _timer?.Dispose();
            
            // Synchronously flush final metrics before disposing HTTP client
            try
            {
                FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                // Best effort - don't throw from Dispose
            }

            _httpClient?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _timer?.Dispose();
            
            // Async flush final metrics before disposing HTTP client
            try
            {
                await FlushAsync(CancellationToken.None);
            }
            catch
            {
                // Best effort - don't throw from Dispose
            }

            _httpClient?.Dispose();
        }
    }
}
