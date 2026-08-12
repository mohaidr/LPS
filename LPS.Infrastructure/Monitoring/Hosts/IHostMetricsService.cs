#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    // Node-aware host metrics router: on a worker it forwards every event to the
    // master over gRPC; on the master (or single node) it applies to the local
    // host aggregator pipeline. Mirrors IMetricsService, but keyed by HostKey.
    public interface IHostMetricsService
    {
        ValueTask IncreaseConnectionsCountAsync(HostKey hostKey, Guid requestId, CancellationToken token);
        ValueTask DecreaseConnectionsCountAsync(HostKey hostKey, CancellationToken token);
        ValueTask IncreaseSkippedRequestsCountAsync(HostKey hostKey, CancellationToken token);
        ValueTask UpdateResponseAsync(HostKey hostKey, HttpResponse.SetupCommand response, CancellationToken token);
        ValueTask UpdateDurationAsync(HostKey hostKey, DurationMetricType metricType, double valueMs, CancellationToken token);
        ValueTask UpdateDataSentAsync(HostKey hostKey, double totalBytes, CancellationToken token);
        ValueTask UpdateDataReceivedAsync(HostKey hostKey, double totalBytes, CancellationToken token);
    }
}
