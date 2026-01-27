using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring
{
    /// <summary>
    /// Fetches metric values from the gRPC metrics service.
    /// Centralized implementation to eliminate code duplication across evaluators.
    /// </summary>
    public class MetricFetcher : IMetricFetcher
    {
        private readonly ICustomGrpcClientFactory _grpcClientFactory;
        private readonly IClusterConfiguration _clusterConfig;

        public MetricFetcher(
            ICustomGrpcClientFactory grpcClientFactory,
            IClusterConfiguration clusterConfig)
        {
            _grpcClientFactory = grpcClientFactory ?? throw new ArgumentNullException(nameof(grpcClientFactory));
            _clusterConfig = clusterConfig ?? throw new ArgumentNullException(nameof(clusterConfig));
        }

        /// <summary>
        /// Gets the current value of a metric by name.
        /// For ErrorRate, uses default filter (>= 400).
        /// </summary>
        public Task<double> GetMetricValueAsync(string fqdn, string metricName, CancellationToken token)
        {
            return GetMetricValueAsync(fqdn, metricName, null, token);
        }

        /// <summary>
        /// Gets the current value of a metric by name with optional status code filter.
        /// </summary>
        public async Task<double> GetMetricValueAsync(string fqdn, string metricName, string errorStatusCodes, CancellationToken token)
        {
            var grpcClient = _grpcClientFactory.GetClient<GrpcMetricsQueryServiceClient>(_clusterConfig.MasterNodeIP);

            // Parse nested metrics like "TotalTime.P90" or "TTFB.Average"
            var parts = metricName.Split('.');
            var baseName = parts[0].ToLower();
            var aggregation = parts.Length > 1 ? parts[1].ToLower() : null;

            return baseName switch
            {
                "errorrate" => await GetErrorRateAsync(grpcClient, fqdn, errorStatusCodes, token),

                // TotalTime metrics
                "totaltime" => await GetTimingMetricAsync(grpcClient, fqdn, TimingMetricType.TotalTime, aggregation, token),

                // TimeToFirstByte metrics
                "ttfb" or "timetofirstbyte" => await GetTimingMetricAsync(grpcClient, fqdn, TimingMetricType.TimeToFirstByte, aggregation, token),

                // WaitingTime metrics (Server processing time: TTFB - TCP - TLS)
                "waitingtime" or "waiting" => await GetTimingMetricAsync(grpcClient, fqdn, TimingMetricType.WaitingTime, aggregation, token),

                // TCPHandshake metrics
                "tcphandshake" or "tcp" => await GetTimingMetricAsync(grpcClient, fqdn, TimingMetricType.TCPHandshake, aggregation, token),

                // TLSHandshake metrics
                "tlshandshake" or "tls" or "ssl" or "sslhandshake" => await GetTimingMetricAsync(grpcClient, fqdn, TimingMetricType.TLSHandshake, aggregation, token),

                // SendingTime (upload) metrics
                "sendingtime" or "sending" or "upload" or "upstream" => await GetTimingMetricAsync(grpcClient, fqdn, TimingMetricType.SendingTime, aggregation, token),

                // ReceivingTime (download) metrics
                "receivingtime" or "receiving" or "download" or "downstream" => await GetTimingMetricAsync(grpcClient, fqdn, TimingMetricType.ReceivingTime, aggregation, token),

                _ => throw new ArgumentException(
                    $"Unknown metric: {metricName}. " +
                    $"Supported metrics: ErrorRate, TotalTime, TTFB, WaitingTime, TCPHandshake, TLSHandshake, SendingTime, ReceivingTime. " +
                    $"Add aggregation for timing metrics: .P50, .P90, .P95, .P99, .Average, .Min, .Max. " +
                    $"Use 'ErrorRate > 0' with 'errorStatusCodes' for status code checks.")
            };
        }

        private enum TimingMetricType
        {
            TotalTime,
            TimeToFirstByte,
            WaitingTime,
            TCPHandshake,
            TLSHandshake,
            SendingTime,
            ReceivingTime
        }

        private async Task<double> GetTimingMetricAsync(
            GrpcMetricsQueryServiceClient grpcClient,
            string fqdn,
            TimingMetricType metricType,
            string? aggregation,
            CancellationToken token)
        {
            var durations = await grpcClient.GetDurationAsync(fqdn, token);
            var duration = durations?.Responses?.SingleOrDefault();

            if (duration == null)
                return 0;

            // Get the appropriate TimingMetricStats based on metric type
            var stats = metricType switch
            {
                TimingMetricType.TotalTime => duration.TotalTime,
                TimingMetricType.TimeToFirstByte => duration.TimeToFirstByte,
                TimingMetricType.WaitingTime => duration.WaitingTime,
                TimingMetricType.TCPHandshake => duration.TcpHandshakeTime,
                TimingMetricType.TLSHandshake => duration.TlsHandshakeTime,
                TimingMetricType.SendingTime => duration.SendingTime,
                TimingMetricType.ReceivingTime => duration.ReceivingTime,
                _ => null
            };

            // Use the stats object to get the metric value
            if (stats != null)
            {
                return GetValueFromStats(stats, aggregation ?? "average");
            }

            return 0;
        }

        /// <summary>
        /// Extracts a specific aggregation value from TimingMetricStats
        /// </summary>
        private static double GetValueFromStats(TimingMetricStats stats, string aggregation)
        {
            return aggregation.ToLower() switch
            {
                "p50" => stats.P50,
                "p90" => stats.P90,
                "p95" => stats.P95,
                "p99" => stats.P99,
                "avg" or "average" => stats.Average,
                "min" => stats.Min,
                "max" => stats.Max,
                "sum" => stats.Sum,
                _ => stats.Average // Default to average
            };
        }

        private async Task<double> GetErrorRateAsync(
            GrpcMetricsQueryServiceClient grpcClient,
            string fqdn,
            string errorStatusCodes,
            CancellationToken token)
        {
            // Always calculate from response codes to apply the filter
            var respCodes = await grpcClient.GetResponseCodesAsync(fqdn, token);
            var respSummaries = respCodes?.Responses?.SingleOrDefault()?.Summaries;

            if (respSummaries == null || respSummaries.Count == 0)
                return 0;

            // Parse the error status codes filter
            // Default: ">= 400" (all client and server errors)
            var filterExpression = string.IsNullOrWhiteSpace(errorStatusCodes) ? ">= 400" : errorStatusCodes;
            
            // Parse the filter - we prepend "StatusCode " to make it a valid metric expression
            var (_, op, threshold, thresholdMax) = MetricParser.Parse($"StatusCode {filterExpression}");

            int total = 0, errors = 0;

            foreach (var s in respSummaries)
            {
                int count = s.Count;
                total += count;

                // Parse status code - can be numeric string ("401") or enum name ("Unauthorized")
                int codeInt = ParseHttpStatusCode(s.HttpStatusCode);
                
                // Check if this status code matches the error filter
                if (RuleService.EvaluateCondition(codeInt, op, threshold, thresholdMax))
                {
                    errors += count;
                }
            }

            return total > 0 ? (double)errors / total : 0;
        }

        /// <summary>
        /// Parses an HTTP status code from string format.
        /// Handles both numeric strings ("401") and enum names ("Unauthorized").
        /// </summary>
        private static int ParseHttpStatusCode(string statusCode)
        {
            if (string.IsNullOrWhiteSpace(statusCode))
                return 0;

            // Try parsing as integer first (e.g., "401")
            if (int.TryParse(statusCode, out var codeInt))
                return codeInt;

            // Try parsing as HttpStatusCode enum name (e.g., "Unauthorized" -> 401)
            if (Enum.TryParse<System.Net.HttpStatusCode>(statusCode, true, out var enumValue))
                return (int)enumValue;

            return 0;
        }
    }
}
