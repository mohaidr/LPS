#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    /// <summary>
    /// Converts LPS metric snapshots to InfluxDB line protocol format.
    /// Line protocol: measurement,tag1=val,tag2=val field1=123,field2=456 timestamp
    /// </summary>
    public static class InfluxDBLineProtocolConverter
    {
        /// <summary>
        /// Converts windowed snapshot to InfluxDB line protocol.
        /// Each metric type (duration, throughput, etc.) becomes separate measurement lines.
        /// </summary>
        public static string ConvertWindowedSnapshot(WindowedIterationSnapshot snapshot)
        {
            var lines = new List<string>();
            var timestamp = ToNanosecondTimestamp(snapshot.WindowEnd);
            var tags = BuildCommonTags(snapshot.PlanName, snapshot.TestStartTime, snapshot.RoundName, snapshot.IterationName, snapshot.TargetUrl);

            // Duration metrics (timing)
            if (snapshot.Duration?.HasData == true)
            {
                lines.AddRange(ConvertWindowedDuration(tags, snapshot.Duration, timestamp));
            }

            // Throughput metrics
            if (snapshot.Throughput?.HasData == true)
            {
                lines.Add(ConvertWindowedThroughput(tags, snapshot.Throughput, timestamp));
            }

            // Response code distribution
            if (snapshot.ResponseCodes?.HasData == true)
            {
                lines.AddRange(ConvertWindowedResponseCodes(tags, snapshot.ResponseCodes, timestamp));
            }

            // Data transmission metrics
            if (snapshot.DataTransmission?.HasData == true)
            {
                lines.Add(ConvertWindowedDataTransmission(tags, snapshot.DataTransmission, timestamp));
            }

            // Final iteration status (only written once when iteration completes)
            if (snapshot.IsFinal)
            {
                lines.Add(ConvertIterationFinalStatus(tags, snapshot.ExecutionStatus, timestamp));
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Converts cumulative snapshot to InfluxDB line protocol.
        /// </summary>
        public static string ConvertCumulativeSnapshot(CumulativeIterationSnapshot snapshot)
        {
            var lines = new List<string>();
            var timestamp = ToNanosecondTimestamp(snapshot.Timestamp);
            var tags = BuildCommonTags(snapshot.PlanName, snapshot.TestStartTime, snapshot.RoundName, snapshot.IterationName, snapshot.TargetUrl);

            // Duration metrics
            if (snapshot.Duration != null)
            {
                lines.AddRange(ConvertCumulativeDuration(tags, snapshot.Duration, timestamp));
            }

            // Throughput metrics
            if (snapshot.Throughput != null)
            {
                lines.Add(ConvertCumulativeThroughput(tags, snapshot.Throughput, timestamp));
            }

            // Response code distribution
            if (snapshot.ResponseCodes != null)
            {
                lines.AddRange(ConvertCumulativeResponseCodes(tags, snapshot.ResponseCodes, timestamp));
            }

            // Data transmission metrics
            if (snapshot.DataTransmission != null)
            {
                lines.Add(ConvertCumulativeDataTransmission(tags, snapshot.DataTransmission, timestamp));
            }

            // Final iteration status (only written once when iteration completes)
            if (snapshot.IsFinal)
            {
                lines.Add(ConvertIterationFinalStatus(tags, snapshot.ExecutionStatus, timestamp));
            }

            return string.Join("\n", lines);
        }

        #region Windowed Conversion Methods

        private static IEnumerable<string> ConvertWindowedDuration(string tags, WindowedDurationData duration, long timestamp)
        {
            var metrics = new Dictionary<string, WindowedTimingMetric>
            {
                ["total_time"] = duration.TotalTime,
                ["tcp_handshake"] = duration.TCPHandshakeTime,
                ["ssl_handshake"] = duration.SSLHandshakeTime,
                ["time_to_first_byte"] = duration.TimeToFirstByte,
                ["waiting_time"] = duration.WaitingTime,
                ["receiving_time"] = duration.ReceivingTime,
                ["sending_time"] = duration.SendingTime
            };

            foreach (var (metricName, metric) in metrics)
            {
                if (metric.Count > 0)
                {
                    yield return BuildLine(
                        "windowed_duration",
                        $"{tags},metric={metricName}",
                        BuildTimingFields(metric),
                        timestamp);
                }
            }
        }

        private static string ConvertWindowedThroughput(string tags, WindowedThroughputData throughput, long timestamp)
        {
            var fields = new StringBuilder();
            // Request counts per window
            fields.Append($"requests_count={throughput.RequestsCount}i,");
            fields.Append($"successful_count={throughput.SuccessfulRequestCount}i,");
            fields.Append($"failed_count={throughput.FailedRequestsCount}i,");
            fields.Append($"max_concurrent_requests={throughput.MaxConcurrentRequests}i");

            return BuildLine("windowed_requests", tags, fields.ToString(), timestamp);
        }

        private static IEnumerable<string> ConvertWindowedResponseCodes(string tags, WindowedResponseCodeData responseCodes, long timestamp)
        {
            foreach (var summary in responseCodes.ResponseSummaries)
            {
                var statusReason = string.IsNullOrWhiteSpace(summary.HttpStatusReason) ? "unknown" : summary.HttpStatusReason;
                var codeTags = $"{tags},status_code={summary.HttpStatusCode},status_reason={EscapeTag(statusReason)}";
                var fields = $"count={summary.Count}i";
                yield return BuildLine("windowed_response_codes", codeTags, fields, timestamp);
            }
        }

        private static string ConvertWindowedDataTransmission(string tags, WindowedDataTransmissionData data, long timestamp)
        {
            var fields = new StringBuilder();
            fields.Append($"data_sent={FormatFloat(data.DataSent)},");
            fields.Append($"data_received={FormatFloat(data.DataReceived)},");
            fields.Append($"upstream_bps={FormatFloat(data.UpstreamThroughputBps)},");
            fields.Append($"downstream_bps={FormatFloat(data.DownstreamThroughputBps)},");
            fields.Append($"total_bps={FormatFloat(data.ThroughputBps)}");

            return BuildLine("windowed_data_transfer", tags, fields.ToString(), timestamp);
        }

        #endregion

        #region Cumulative Conversion Methods

        private static IEnumerable<string> ConvertCumulativeDuration(string tags, CumulativeDurationData duration, long timestamp)
        {
            var metrics = new Dictionary<string, CumulativeTimingMetric?>
            {
                ["total_time"] = duration.TotalTime,
                ["tcp_handshake"] = duration.TCPHandshakeTime,
                ["ssl_handshake"] = duration.SSLHandshakeTime,
                ["time_to_first_byte"] = duration.TimeToFirstByte,
                ["waiting_time"] = duration.WaitingTime,
                ["receiving_time"] = duration.ReceivingTime,
                ["sending_time"] = duration.SendingTime
            };

            foreach (var (metricName, metric) in metrics)
            {
                if (metric != null)
                {
                    yield return BuildLine(
                        "cumulative_duration",
                        $"{tags},metric={metricName}",
                        BuildCumulativeTimingFields(metric),
                        timestamp);
                }
            }
        }

        private static string ConvertCumulativeThroughput(string tags, CumulativeThroughputData throughput, long timestamp)
        {
            var fields = new StringBuilder();
            // Request counts
            fields.Append($"requests_count={throughput.RequestsCount}i,");
            fields.Append($"successful_count={throughput.SuccessfulRequestCount}i,");
            fields.Append($"failed_count={throughput.FailedRequestsCount}i,");
            fields.Append($"max_concurrent_requests={throughput.MaxConcurrentRequests}i,");
            // Calculated rates
            fields.Append($"requests_per_second={FormatFloat(throughput.RequestsPerSecond)},");
            fields.Append($"requests_rate_per_cooldown={FormatFloat(throughput.RequestsRatePerCoolDown)},");
            fields.Append($"error_rate={FormatFloat(throughput.ErrorRate)},");
            fields.Append($"time_elapsed_ms={FormatFloat(throughput.TimeElapsedMs)}");

            return BuildLine("cumulative_requests", tags, fields.ToString(), timestamp);
        }

        private static IEnumerable<string> ConvertCumulativeResponseCodes(string tags, CumulativeResponseCodeData responseCodes, long timestamp)
        {
            foreach (var summary in responseCodes.ResponseSummaries)
            {
                var statusReason = string.IsNullOrWhiteSpace(summary.HttpStatusReason) ? "unknown" : summary.HttpStatusReason;
                var codeTags = $"{tags},status_code={summary.HttpStatusCode},status_reason={EscapeTag(statusReason)}";
                var fields = $"count={summary.Count}i";
                yield return BuildLine("cumulative_response_codes", codeTags, fields, timestamp);
            }
        }

        private static string ConvertCumulativeDataTransmission(string tags, CumulativeDataTransmissionData data, long timestamp)
        {
            var fields = new StringBuilder();
            fields.Append($"data_sent={FormatFloat(data.DataSent)},");
            fields.Append($"data_received={FormatFloat(data.DataReceived)},");
            fields.Append($"avg_data_sent={FormatFloat(data.AverageDataSent)},");
            fields.Append($"avg_data_received={FormatFloat(data.AverageDataReceived)},");
            fields.Append($"upstream_bps={FormatFloat(data.UpstreamThroughputBps)},");
            fields.Append($"downstream_bps={FormatFloat(data.DownstreamThroughputBps)},");
            fields.Append($"total_bps={FormatFloat(data.ThroughputBps)}");

            return BuildLine("cumulative_data_transfer", tags, fields.ToString(), timestamp);
        }

        /// <summary>
        /// Converts iteration final status to InfluxDB line protocol.
        /// Only written when IsFinal==true. Status is a field (not a tag) so it won't appear as a filter.
        /// </summary>
        private static string ConvertIterationFinalStatus(string tags, string executionStatus, long timestamp)
        {
            var safeStatus = string.IsNullOrWhiteSpace(executionStatus) ? "unknown" : executionStatus;
            var fields = $"status=\"{EscapeFieldValue(safeStatus)}\"";
            return BuildLine("iteration_final_status", tags, fields, timestamp);
        }

        #endregion

        #region Helper Methods

        private static string BuildCommonTags(string planName, DateTime testStartTime, string roundName, string iterationName, string targetUrl)
        {
            // InfluxDB doesn't allow empty tag values, use "unknown" as fallback
            var safePlanName = string.IsNullOrWhiteSpace(planName) ? "unknown" : planName;
            var safeRoundName = string.IsNullOrWhiteSpace(roundName) ? "unknown" : roundName;
            var safeIterationName = string.IsNullOrWhiteSpace(iterationName) ? "unknown" : iterationName;
            var safeTargetUrl = string.IsNullOrWhiteSpace(targetUrl) ? "unknown" : targetUrl;
            
            // Append test start time to plan name for unique identification
            // Format: PlanName_2026-01-09_03-45-23 (tag-friendly, no colons)
            var testStartTimeStr = testStartTime.ToString("yyyy-MM-dd_HH-mm-ss");
            var planNameWithTimestamp = $"{safePlanName}_{testStartTimeStr}";
            
            // Tag order matters in InfluxDB UI: plan → round → iteration → target
            return $"plan_name={EscapeTag(planNameWithTimestamp)}," +
                   $"round_name={EscapeTag(safeRoundName)}," +
                   $"iteration_name={EscapeTag(safeIterationName)}," +
                   $"target={EscapeTag(safeTargetUrl)}";
        }

        private static string BuildTimingFields(WindowedTimingMetric metric)
        {
            return $"sum={FormatFloat(metric.Sum)}," +
                   $"avg={FormatFloat(metric.Average)}," +
                   $"min={FormatFloat(metric.Min)}," +
                   $"max={FormatFloat(metric.Max)}," +
                   $"p50={FormatFloat(metric.P50)}," +
                   $"p90={FormatFloat(metric.P90)}," +
                   $"p95={FormatFloat(metric.P95)}," +
                   $"p99={FormatFloat(metric.P99)}";
        }

        private static string BuildCumulativeTimingFields(CumulativeTimingMetric metric)
        {
            return $"sum={FormatFloat(metric.Sum)}," +
                   $"avg={FormatFloat(metric.Average)}," +
                   $"min={FormatFloat(metric.Min)}," +
                   $"max={FormatFloat(metric.Max)}," +
                   $"p50={FormatFloat(metric.P50)}," +
                   $"p90={FormatFloat(metric.P90)}," +
                   $"p95={FormatFloat(metric.P95)}," +
                   $"p99={FormatFloat(metric.P99)}";
        }

        private static string BuildLine(string measurement, string tags, string fields, long timestamp)
        {
            return $"{measurement},{tags} {fields} {timestamp}";
        }

        private static string EscapeTag(string value)
        {
            return value
                .Replace(",", "\\,")
                .Replace("=", "\\=")
                .Replace(" ", "\\ ");
        }

        private static string EscapeFieldValue(string value)
        {
            // String field values need quotes escaped
            return value.Replace("\"", "\\\"");
        }

        private static string FormatFloat(double value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static long ToNanosecondTimestamp(DateTime dateTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(dateTime.ToUniversalTime() - epoch).TotalMilliseconds * 1_000_000;
        }

        #endregion
    }
}
