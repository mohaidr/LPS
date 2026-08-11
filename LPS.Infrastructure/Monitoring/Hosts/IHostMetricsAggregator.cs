#nullable enable
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    public interface IHostMetricsAggregator
    {
        HostKey HostKey { get; }

        ValueTask IncreaseConnectionsCountAsync(CancellationToken token);
        ValueTask DecreaseConnectionsCountAsync(CancellationToken token);
        ValueTask IncreaseSkippedRequestsCountAsync(CancellationToken token);
        ValueTask UpdateResponseAsync(HttpResponse.SetupCommand response, CancellationToken token);
        ValueTask UpdateDurationAsync(DurationMetricType metricType, double valueMs, CancellationToken token);
        ValueTask UpdateDataSentAsync(double totalBytes, CancellationToken token);
        ValueTask UpdateDataReceivedAsync(double totalBytes, CancellationToken token);
        HostCumulativeMetricsSnapshot GetCumulativeSnapshot();
        HostWindowedMetricsSnapshot GetWindowedSnapshotAndReset();
    }

    public interface IHostMetricsAggregatorFactory
    {
        IHostMetricsAggregator GetOrCreate(System.Uri targetUri);
        IHostMetricsAggregator GetOrCreate(System.Uri targetUri, System.Guid requestId);
        bool TryGet(HostKey hostKey, out IHostMetricsAggregator aggregator);
    }
}