#nullable enable
using System;
using System.Threading;
using LPS.Domain;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.Cumulative
{
    /// <summary>
    /// Collector for cumulative metrics for a single iteration.
    /// Subscribes to cumulative coordinator's push interval event,
    /// collects cumulative data directly from the aggregators (like WindowedIterationMetricsCollector),
    /// pushes to queue for real-time streaming, and stores in data store for persistence.
    /// Self-manages lifecycle based on iteration status from IIterationStatusMonitor.
    /// </summary>
    public sealed class CumulativeIterationMetricsCollector : IDisposable
    {
        private readonly HttpIteration _httpIteration;
        private readonly string _roundName;
        private readonly ICumulativeMetricsQueue _queue;
        private readonly ICumulativeMetricDataStore _dataStore;
        private readonly ICumulativeMetricsCoordinator _coordinator;
        private readonly IIterationStatusMonitor _iterationStatusMonitor;
        private readonly IPlanExecutionContext _planContext;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private bool _disposed;
        private bool _finalSnapshotSent;

        // Component aggregators (set after construction by MetricsDataMonitor)
        public ThroughputMetricAggregator? ThroughputAggregator { get; set; }
        public DurationMetricAggregator? DurationAggregator { get; set; }
        public ResponseCodeMetricAggregator? ResponseCodeAggregator { get; set; }
        public DataTransmissionMetricAggregator? DataTransmissionAggregator { get; set; }

        public HttpIteration HttpIteration => _httpIteration;
        
        public CumulativeIterationMetricsCollector(
            HttpIteration httpIteration,
            string roundName,
            ICumulativeMetricsQueue queue,
            ICumulativeMetricDataStore dataStore,
            ICumulativeMetricsCoordinator coordinator,
            IIterationStatusMonitor iterationStatusMonitor,
            IPlanExecutionContext planContext)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _iterationStatusMonitor = iterationStatusMonitor ?? throw new ArgumentNullException(nameof(iterationStatusMonitor));
            _planContext = planContext ?? throw new ArgumentNullException(nameof(planContext));

            // Subscribe to push interval events
            _coordinator.OnPushInterval += OnPushInterval;
        }

        private async void OnPushInterval()
        {
            if (_disposed || _finalSnapshotSent) return;

            try
            {
                // Get authoritative status from IIterationStatusMonitor
                var status = await _iterationStatusMonitor.GetTerminalStatusAsync(_httpIteration, CancellationToken.None);

                switch (status)
                {
                    case EntityExecutionStatus.NotStarted:
                    case EntityExecutionStatus.Scheduled:
                        // No data yet - skip
                        return;

                    case EntityExecutionStatus.Ongoing:
                    case EntityExecutionStatus.OngingScehduled:
                    case EntityExecutionStatus.PartiallySkipped:
                        // Active - push regular snapshot
                        // PartiallySkipped means some clients skipped but iteration is still running
                        PushSnapshot(isFinal: false, status.ToString());
                        break;

                    case EntityExecutionStatus.Success:
                    case EntityExecutionStatus.Failed:
                    case EntityExecutionStatus.Cancelled:
                    case EntityExecutionStatus.Terminated:
                    case EntityExecutionStatus.Skipped:
                        // Terminal - push final and cleanup
                        _finalSnapshotSent = true;
                        PushSnapshot(isFinal: true, status.ToString());
                        Dispose(); // Unsubscribe from coordinator
                        break;
                }
            }
            catch
            {
                // Swallow exceptions to prevent coordinator timer from dying
            }
        }

        private void PushSnapshot(bool isFinal, string executionStatus)
        {
            _semaphore.Wait();
            try
            {
                // Collect cumulative data directly from aggregators (like WindowedIterationMetricsCollector)
                string? targetUrl = null;
                var throughput = ThroughputAggregator?.GetCumulativeData(out targetUrl);
                var duration = DurationAggregator?.GetCumulativeData();
                var dataTransmission = DataTransmissionAggregator?.GetCumulativeData();
                var responseCodes = ResponseCodeAggregator?.GetCumulativeData();

                // Get target URL from throughput aggregator or fall back to iteration's URL
                string url = targetUrl ?? _httpIteration.HttpRequest?.Url?.Url ?? string.Empty;

                var snapshot = new CumulativeIterationSnapshot
                {
                    IterationId = _httpIteration.Id,
                    PlanName = _planContext.PlanName,
                    TestStartTime = _planContext.TestStartTime,
                    RoundName = _roundName,
                    IterationName = _httpIteration.Name,
                    TargetUrl = url,
                    Timestamp = DateTime.UtcNow,
                    ExecutionStatus = executionStatus,
                    IsFinal = isFinal,
                    Throughput = throughput,
                    Duration = duration,
                    DataTransmission = dataTransmission,
                    ResponseCodes = responseCodes
                };

                // Push if final or has any cumulative data
                if (isFinal || snapshot.HasData)
                {
                    _queue.TryEnqueue(snapshot);
                    // Also store for persistence
                    _ = _dataStore.PushAsync(_httpIteration.Id, snapshot);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _coordinator.OnPushInterval -= OnPushInterval;
            _semaphore.Dispose();
        }
    }
}
