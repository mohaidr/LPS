#nullable enable
using System;
using System.Collections.Generic;

namespace LPS.Infrastructure.Monitoring.Cumulative
{
    /// <summary>
    /// Snapshot containing only cumulative metrics for an iteration.
    /// Pushed at the cumulative refresh rate interval.
    /// </summary>
    public sealed class CumulativeIterationSnapshot
    {
        public Guid IterationId { get; init; }
        public string PlanName { get; init; } = string.Empty;
        public DateTime TestStartTime { get; init; }
        public string RoundName { get; init; } = string.Empty;
        public string IterationName { get; init; } = string.Empty;
        public string TargetUrl { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string ExecutionStatus { get; init; } = "Ongoing";
        public bool IsFinal { get; init; }

        // Cumulative throughput data
        public CumulativeThroughputData? Throughput { get; init; }

        // Cumulative duration/timing data
        public CumulativeDurationData? Duration { get; init; }

        // Cumulative data transmission data
        public CumulativeDataTransmissionData? DataTransmission { get; init; }

        // Cumulative response code data
        public CumulativeResponseCodeData? ResponseCodes { get; init; }

        public bool HasData =>
            Throughput != null ||
            Duration != null ||
            DataTransmission != null ||
            ResponseCodes != null;
    }

    public sealed class CumulativeThroughputData
    {
        public long RequestsCount { get; init; }
        public long SuccessfulRequestCount { get; init; }
        public long FailedRequestsCount { get; init; }
        public long ActiveRequestsCount { get; init; }
        public double RequestsPerSecond { get; init; }
        public double RequestsRatePerCoolDown { get; init; }
        public double ErrorRate { get; init; }
        public double TimeElapsedMs { get; init; }
    }

    public sealed class CumulativeDurationData
    {
        public CumulativeTimingMetric? TotalTime { get; init; }
        public CumulativeTimingMetric? TCPHandshakeTime { get; init; }
        public CumulativeTimingMetric? SSLHandshakeTime { get; init; }
        public CumulativeTimingMetric? TimeToFirstByte { get; init; }
        public CumulativeTimingMetric? WaitingTime { get; init; }
        public CumulativeTimingMetric? ReceivingTime { get; init; }
        public CumulativeTimingMetric? SendingTime { get; init; }
    }

    public sealed class CumulativeTimingMetric
    {
        public double Sum { get; init; }
        public double Average { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }
        public double P50 { get; init; }
        public double P90 { get; init; }
        public double P95 { get; init; }
        public double P99 { get; init; }
    }

    public sealed class CumulativeDataTransmissionData
    {
        public double DataSent { get; init; }
        public double DataReceived { get; init; }
        public double AverageDataSent { get; init; }
        public double AverageDataReceived { get; init; }
        public double UpstreamThroughputBps { get; init; }
        public double DownstreamThroughputBps { get; init; }
        public double ThroughputBps { get; init; }
    }

    public sealed class CumulativeResponseCodeData
    {
        public List<CumulativeResponseSummary> ResponseSummaries { get; init; } = new();
    }

    public sealed class CumulativeResponseSummary
    {
        public int HttpStatusCode { get; init; }
        public string HttpStatusReason { get; init; } = string.Empty;
        public long Count { get; init; }
    }
}
