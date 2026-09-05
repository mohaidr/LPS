#nullable enable
using System;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    /// <summary>Rule-based success/failure totals folded from a host's iterations.</summary>
    public readonly record struct HostFailureCounts(long Successful, long Failed);

    public sealed class HostCumulativeMetricsSnapshot
    {
        public HostKey HostKey { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string ExecutionStatus { get; set; } = "Ongoing";
        public bool IsFinal { get; set; }
        public CumulativeThroughputData Throughput { get; init; } = new();
        public CumulativeDurationData Duration { get; init; } = new();
        public CumulativeDataTransmissionData DataTransmission { get; init; } = new();
        public CumulativeResponseCodeData ResponseCodes { get; init; } = new();
    }

    public sealed class HostWindowedMetricsSnapshot
    {
        public HostKey HostKey { get; init; }
        public DateTime WindowStart { get; init; }
        public DateTime WindowEnd { get; init; }
        public string ExecutionStatus { get; set; } = "Ongoing";
        public bool IsFinal { get; set; }
        public WindowedThroughputData Throughput { get; init; } = new();
        public WindowedDurationData Duration { get; init; } = new();
        public WindowedDataTransmissionData DataTransmission { get; init; } = new();
        public WindowedResponseCodeData ResponseCodes { get; init; } = new();
    }
}