using LPS.Infrastructure.Monitoring.Metrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.DTOs
{
    public sealed class MetricDataDto
    {
        public DateTime TimeStamp { get; init; }
        public string URL { get; init; } = string.Empty;
        public string HttpMethod { get; init; } = string.Empty;
        public string HttpVersion { get; init; } = string.Empty;

        public string RoundName { get; init; } = string.Empty;
        public Guid IterationId { get; init; }
        public string IterationName { get; init; } = string.Empty;

        public string Endpoint { get; init; } = string.Empty;
        public string ExecutionStatus { get; init; } = string.Empty;

        public ResponseCodeMetricSnapshot? ResponseBreakDownMetrics { get; init; }
        public DurationMetricSnapshot? ResponseTimeMetrics { get; init; }
        public ThroughputMetricSnapshot? ConnectionMetrics { get; init; }
        public DataTransmissionMetricSnapshot? DataTransmissionMetrics { get; init; }
    }
}
