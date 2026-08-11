#nullable enable
using System;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostCumulativeMetricsCollector : IDisposable
    {
        private readonly IHostMetricsAggregator _aggregator;
        private readonly IHostCumulativeMetricsQueue _queue;
        private readonly ICumulativeMetricsCoordinator _coordinator;
        private readonly HostExecutionStatusTracker? _executionStatus;
        private bool _disposed;

        public HostCumulativeMetricsCollector(
            IHostMetricsAggregator aggregator,
            IHostCumulativeMetricsQueue queue,
            ICumulativeMetricsCoordinator coordinator,
            HostExecutionStatusTracker? executionStatus = null)
        {
            _aggregator = aggregator;
            _queue = queue;
            _coordinator = coordinator;
            _executionStatus = executionStatus;
            _coordinator.OnPushInterval += OnPushInterval;
        }

        private void OnPushInterval()
        {
            if (!_disposed)
            {
                var snapshot = _aggregator.GetCumulativeSnapshot();
                ApplyStatus(snapshot, _executionStatus?.GetStatus(_aggregator.HostKey));
                _queue.TryEnqueue(snapshot);
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
        private bool _disposed;

        public HostWindowedMetricsCollector(
            IHostMetricsAggregator aggregator,
            IHostWindowedMetricsQueue queue,
            IWindowedMetricsCoordinator coordinator,
            HostExecutionStatusTracker? executionStatus = null)
        {
            _aggregator = aggregator;
            _queue = queue;
            _coordinator = coordinator;
            _executionStatus = executionStatus;
            _coordinator.OnWindowClosed += OnWindowClosed;
        }

        private void OnWindowClosed()
        {
            if (!_disposed)
            {
                var snapshot = _aggregator.GetWindowedSnapshotAndReset();
                ApplyStatus(snapshot, _executionStatus?.GetStatus(_aggregator.HostKey));
                _queue.TryEnqueue(snapshot);
            }
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
}