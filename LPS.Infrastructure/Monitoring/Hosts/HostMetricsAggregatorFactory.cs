#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    public sealed class HostMetricsAggregatorFactory : IHostMetricsAggregatorFactory, IDisposable
    {
        private sealed record Entry(
            IHostMetricsAggregator Aggregator,
            HostWindowedMetricsCollector? WindowedCollector,
            HostCumulativeMetricsCollector? CumulativeCollector);

        private readonly ConcurrentDictionary<HostKey, Lazy<Entry>> _aggregators = new();
        private readonly IHostWindowedMetricsQueue? _windowedQueue;
        private readonly IWindowedMetricsCoordinator? _windowedCoordinator;
        private readonly IHostCumulativeMetricsQueue? _cumulativeQueue;
        private readonly ICumulativeMetricsCoordinator? _cumulativeCoordinator;
        private readonly IMetricAggregatorFactory? _metricAggregatorFactory;
        private readonly HostExecutionStatusTracker? _executionStatus;
        private bool _disposed;

        public HostMetricsAggregatorFactory()
        {
        }

        public HostMetricsAggregatorFactory(
            IHostWindowedMetricsQueue windowedQueue,
            IWindowedMetricsCoordinator windowedCoordinator,
            IHostCumulativeMetricsQueue cumulativeQueue,
            ICumulativeMetricsCoordinator cumulativeCoordinator,
            IMetricAggregatorFactory metricAggregatorFactory,
            IIterationStatusMonitor iterationStatusMonitor)
        {
            _windowedQueue = windowedQueue;
            _windowedCoordinator = windowedCoordinator;
            _cumulativeQueue = cumulativeQueue;
            _cumulativeCoordinator = cumulativeCoordinator;
            _metricAggregatorFactory = metricAggregatorFactory;
            _executionStatus = new HostExecutionStatusTracker(iterationStatusMonitor);
        }

        public IHostMetricsAggregator GetOrCreate(Uri targetUri)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var hostKey = HostKey.From(targetUri);
            return _aggregators.GetOrAdd(
                hostKey,
                key => new Lazy<Entry>(
                    () => CreateEntry(key),
                    isThreadSafe: true)).Value.Aggregator;
        }

        public IHostMetricsAggregator GetOrCreate(Uri targetUri, Guid requestId)
        {
            var aggregator = GetOrCreate(targetUri);
            var iteration = _metricAggregatorFactory?.Iterations
                .SingleOrDefault(candidate => candidate.HttpRequest.Id == requestId);

            if (iteration != null)
                _executionStatus?.Track(aggregator.HostKey, iteration);

            return aggregator;
        }

        public bool TryGet(HostKey hostKey, out IHostMetricsAggregator aggregator)
        {
            if (_aggregators.TryGetValue(hostKey, out var lazy))
            {
                aggregator = lazy.Value.Aggregator;
                return true;
            }

            aggregator = null!;
            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var lazy in _aggregators.Values)
            {
                if (!lazy.IsValueCreated) continue;

                lazy.Value.WindowedCollector?.Dispose();
                lazy.Value.CumulativeCollector?.Dispose();
                if (lazy.Value.Aggregator is IDisposable disposable)
                    disposable.Dispose();
            }

            _aggregators.Clear();
        }

        private Entry CreateEntry(HostKey hostKey)
        {
            var aggregator = new HostMetricsAggregator(hostKey);
            var windowedCollector = _windowedQueue != null && _windowedCoordinator != null
                ? new HostWindowedMetricsCollector(aggregator, _windowedQueue, _windowedCoordinator, _executionStatus)
                : null;
            var cumulativeCollector = _cumulativeQueue != null && _cumulativeCoordinator != null
                ? new HostCumulativeMetricsCollector(aggregator, _cumulativeQueue, _cumulativeCoordinator, _executionStatus)
                : null;

            return new Entry(aggregator, windowedCollector, cumulativeCollector);
        }
    }
}