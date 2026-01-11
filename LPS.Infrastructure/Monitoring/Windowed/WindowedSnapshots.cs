#nullable enable
using System;
using System.Collections.Generic;
using System.Net;

namespace LPS.Infrastructure.Monitoring.Windowed
{
    /// <summary>
    /// Immutable snapshot containing windowed metrics for a single iteration.
    /// Windowed = current window period (for charts showing trends over time).
    /// </summary>
    public class WindowedIterationSnapshot
    {
        public Guid IterationId { get; init; }
        public string PlanName { get; init; } = string.Empty;
        public DateTime TestStartTime { get; init; }
        public string RoundName { get; init; } = string.Empty;
        public string IterationName { get; init; } = string.Empty;
        public string TargetUrl { get; init; } = string.Empty;
        public int WindowSequence { get; init; }
        public DateTime WindowStart { get; init; }
        public DateTime WindowEnd { get; init; }
        
        /// <summary>
        /// Execution status of the iteration (e.g., "Ongoing", "Success", "Failed").
        /// </summary>
        public string ExecutionStatus { get; init; } = "Ongoing";
        
        /// <summary>
        /// Indicates if this is the final snapshot for this iteration.
        /// Frontend should update status display when this is true.
        /// </summary>
        public bool IsFinal { get; init; }

        // Windowed data (for charts - trends over time)
        public WindowedDurationData? Duration { get; init; }
        public WindowedThroughputData? Throughput { get; init; }
        public WindowedResponseCodeData? ResponseCodes { get; init; }
        public WindowedDataTransmissionData? DataTransmission { get; init; }

        public bool HasData => Duration != null || Throughput != null || 
                               ResponseCodes != null || DataTransmission != null;
    }

    #region Windowed Data Types (for charts)

    /// <summary>
    /// Windowed duration/timing metrics.
    /// </summary>
    public class WindowedDurationData
    {
        public WindowedTimingMetric TotalTime { get; init; } = new();
        public WindowedTimingMetric TCPHandshakeTime { get; init; } = new();
        public WindowedTimingMetric SSLHandshakeTime { get; init; } = new();
        public WindowedTimingMetric TimeToFirstByte { get; init; } = new();
        public WindowedTimingMetric WaitingTime { get; init; } = new();
        public WindowedTimingMetric ReceivingTime { get; init; } = new();
        public WindowedTimingMetric SendingTime { get; init; } = new();

        public bool HasData => TotalTime.Count > 0;
    }

    /// <summary>
    /// Single timing metric with percentiles.
    /// </summary>
    public class WindowedTimingMetric
    {
        public int Count { get; init; }
        public double Sum { get; init; }
        public double Average { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }
        public double P50 { get; init; }
        public double P90 { get; init; }
        public double P95 { get; init; }
        public double P99 { get; init; }
    }

    /// <summary>
    /// Windowed throughput metrics.
    /// </summary>
    public class WindowedThroughputData
    {
        public int RequestsCount { get; init; }
        public int SuccessfulRequestCount { get; init; }
        public int FailedRequestsCount { get; init; }
        public int ActiveRequestsCount { get; init; }
        public double RequestsPerSecond { get; init; }
        public double ErrorRate { get; init; }

        public bool HasData => RequestsCount > 0;
    }

    /// <summary>
    /// Windowed response code metrics.
    /// </summary>
    public class WindowedResponseCodeData
    {
        public List<WindowedResponseSummary> ResponseSummaries { get; init; } = new();

        public bool HasData => ResponseSummaries.Count > 0;
    }

    /// <summary>
    /// Single response code count.
    /// </summary>
    public class WindowedResponseSummary
    {
        public HttpStatusCode HttpStatusCode { get; init; }
        public string HttpStatusReason { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    /// <summary>
    /// Windowed data transmission metrics.
    /// </summary>
    public class WindowedDataTransmissionData
    {
        public double DataSent { get; init; }
        public double DataReceived { get; init; }
        public double UpstreamThroughputBps { get; init; }
        public double DownstreamThroughputBps { get; init; }
        public double ThroughputBps { get; init; }

        public bool HasData => DataSent > 0 || DataReceived > 0;
    }

    #endregion
}
