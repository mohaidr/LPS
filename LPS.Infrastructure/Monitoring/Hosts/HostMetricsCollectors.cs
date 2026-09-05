#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostCumulativeMetricsCollector : IDisposable
    {
        private readonly IHostMetricsAggregator _aggregator;
        private readonly IHostCumulativeMetricsQueue _queue;
        private readonly ICumulativeMetricsCoordinator _coordinator;
        private readonly HostExecutionStatusTracker? _executionStatus;
        private readonly ILiveMetricDataStore? _metricDataStore;
        private readonly IMetricAggregatorFactory? _metricAggregatorFactory;
        private int _pushInProgress;
        private bool _finalReconcileDone;
        private bool _disposed;

        public HostCumulativeMetricsCollector(
            IHostMetricsAggregator aggregator,
            IHostCumulativeMetricsQueue queue,
            ICumulativeMetricsCoordinator coordinator,
            HostExecutionStatusTracker? executionStatus = null,
            ILiveMetricDataStore? metricDataStore = null,
            IMetricAggregatorFactory? metricAggregatorFactory = null)
        {
            _aggregator = aggregator;
            _queue = queue;
            _coordinator = coordinator;
            _executionStatus = executionStatus;
            _metricDataStore = metricDataStore;
            _metricAggregatorFactory = metricAggregatorFactory;
            _coordinator.OnPushInterval += OnPushInterval;
        }

        private async Task OnPushInterval()
        {
            if (_disposed || Interlocked.Exchange(ref _pushInProgress, 1) != 0) return;
            try
            {
                var status = _executionStatus?.GetStatus(_aggregator.HostKey);

                // Once the host completes, reconcile its iterations so the fold reads their final totals
                // instead of racing the iterations' own async reconcile.
                if (status == HostExecutionStatus.Completed && !_finalReconcileDone)
                {
                    _finalReconcileDone = true;
                    await ReconcileIterationsAsync().ConfigureAwait(false);
                }

                if (_disposed) return;

                var counts = HostThroughputRollup.Fold(_aggregator.HostKey, _executionStatus, _metricDataStore);
                var snapshot = _aggregator.GetCumulativeSnapshot(counts);
                ApplyStatus(snapshot, status);
                _queue.TryEnqueue(snapshot);
            }
            finally
            {
                Volatile.Write(ref _pushInProgress, 0);
            }
        }

        private async Task ReconcileIterationsAsync()
        {
            if (_executionStatus is null || _metricAggregatorFactory is null) return;

            foreach (var iterationId in _executionStatus.GetIterationIds(_aggregator.HostKey))
            {
                if (!_metricAggregatorFactory.TryGet(iterationId, out var aggregators)) continue;
                try
                {
                    if (aggregators.OfType<ResponseCodeMetricAggregator>().FirstOrDefault() is { } rc)
                        await rc.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                    if (aggregators.OfType<ThroughputMetricAggregator>().FirstOrDefault() is { } tp)
                        await tp.ReconcileAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch { /* best-effort reconcile */ }
            }
        }

        private static void ApplyStatus(
            HostCumulativeMetricsSnapshot snapshot,
            HostExecutionStatus? executionStatus)
        {
            var status = executionStatus ?? HostExecutionStatus.Ongoing;
            snapshot.ExecutionStatus = status.ToString();
            snapshot.IsFinal = status == HostExecutionStatus.Completed;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _coordinator.OnPushInterval -= OnPushInterval;
        }
    }

    internal sealed class HostWindowedMetricsCollector : IDisposable
    {
        private readonly IHostMetricsAggregator _aggregator;
        private readonly IHostWindowedMetricsQueue _queue;
        private readonly IWindowedMetricsCoordinator _coordinator;
        private readonly HostExecutionStatusTracker? _executionStatus;
        private readonly ILiveMetricDataStore? _metricDataStore;
        private long _lastSuccessful;
        private long _lastFailed;
        private bool _disposed;

        public HostWindowedMetricsCollector(
            IHostMetricsAggregator aggregator,
            IHostWindowedMetricsQueue queue,
            IWindowedMetricsCoordinator coordinator,
            HostExecutionStatusTracker? executionStatus = null,
            ILiveMetricDataStore? metricDataStore = null)
        {
            _aggregator = aggregator;
            _queue = queue;
            _coordinator = coordinator;
            _executionStatus = executionStatus;
            _metricDataStore = metricDataStore;
            _coordinator.OnWindowClosed += OnWindowClosed;
        }

        private Task OnWindowClosed()
        {
            if (!_disposed)
            {
                // Iteration throughput totals are cumulative; the window delta is the growth since the last window.
                var total = HostThroughputRollup.Fold(_aggregator.HostKey, _executionStatus, _metricDataStore);
                var windowCounts = new HostFailureCounts(
                    Math.Max(0, total.Successful - _lastSuccessful),
                    Math.Max(0, total.Failed - _lastFailed));
                _lastSuccessful = total.Successful;
                _lastFailed = total.Failed;

                var snapshot = _aggregator.GetWindowedSnapshotAndReset(windowCounts);
                ApplyStatus(snapshot, _executionStatus?.GetStatus(_aggregator.HostKey));
                _queue.TryEnqueue(snapshot);
            }

            return Task.CompletedTask;
        }

        private static void ApplyStatus(
            HostWindowedMetricsSnapshot snapshot,
            HostExecutionStatus? executionStatus)
        {
            var status = executionStatus ?? HostExecutionStatus.Ongoing;
            snapshot.ExecutionStatus = status.ToString();
            snapshot.IsFinal = status == HostExecutionStatus.Completed;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _coordinator.OnWindowClosed -= OnWindowClosed;
        }
    }

    // Folds the rule-based success/failure totals of a host's iterations from the live store,
    // so host metrics reflect the sum of iteration results instead of classifying responses themselves.
    internal static class HostThroughputRollup
    {
        public static HostFailureCounts Fold(
            HostKey hostKey,
            HostExecutionStatusTracker? tracker,
            ILiveMetricDataStore? store)
        {
            if (tracker is null || store is null)
                return default;

            long successful = 0;
            long failed = 0;
            foreach (var iterationId in tracker.GetIterationIds(hostKey))
            {
                if (store.TryGetLatest<ThroughputMetricSnapshot>(iterationId, LPSMetricType.Throughput, out var snapshot)
                    && snapshot is not null)
                {
                    successful += snapshot.SuccessfulRequestCount;
                    failed += snapshot.FailedRequestsCount;
                }
            }

            return new HostFailureCounts(successful, failed);
        }
    }
}