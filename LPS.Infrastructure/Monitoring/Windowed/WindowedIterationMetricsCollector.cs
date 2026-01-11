#nullable enable
using System;
using System.Threading;
using LPS.Domain;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;

namespace LPS.Infrastructure.Monitoring.Windowed
{
    /// <summary>
    /// Unified windowed metrics collector for a single iteration.
    /// Subscribes to coordinator's window close event, collects data from all
    /// windowed aggregators, builds a single unified snapshot, and pushes to queue.
    /// Handles windowed data only - cumulative data is handled by CumulativeIterationMetricsCollector.
    /// Self-manages lifecycle based on iteration status from IIterationStatusMonitor.
    /// </summary>
    public sealed class WindowedIterationMetricsCollector : IDisposable
    {
        private readonly HttpIteration _httpIteration;
        private readonly string _roundName;
        private readonly IWindowedMetricsQueue _queue;
        private readonly IWindowedMetricsCoordinator _coordinator;
        private readonly IIterationStatusMonitor _iterationStatusMonitor;
        private readonly IPlanExecutionContext _planContext;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private int _windowSequence;
        private DateTime _windowStart = DateTime.UtcNow;
        private bool _disposed;
        private bool _finalSnapshotSent;

        // Component aggregators (set after construction)
        public WindowedDurationAggregator? DurationAggregator { get; set; }
        public WindowedThroughputAggregator? ThroughputAggregator { get; set; }
        public WindowedResponseCodeAggregator? ResponseCodeAggregator { get; set; }
        public WindowedDataTransmissionAggregator? DataTransmissionAggregator { get; set; }

        public HttpIteration HttpIteration => _httpIteration;

        public WindowedIterationMetricsCollector(
            HttpIteration httpIteration,
            string roundName,
            IWindowedMetricsQueue queue,
            IWindowedMetricsCoordinator coordinator,
            IIterationStatusMonitor iterationStatusMonitor,
            IPlanExecutionContext planContext)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _iterationStatusMonitor = iterationStatusMonitor ?? throw new ArgumentNullException(nameof(iterationStatusMonitor));
            _planContext = planContext ?? throw new ArgumentNullException(nameof(planContext));

            // Subscribe to window close events
            _coordinator.OnWindowClosed += OnWindowClosed;
        }

        private async void OnWindowClosed()
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
                var windowEnd = DateTime.UtcNow;
                var windowSequence = ++_windowSequence;

                // Collect windowed data from aggregators (don't reset for final snapshot)
                WindowedDurationData? durationData = null;
                WindowedThroughputData? throughputData = null;
                WindowedResponseCodeData? responseCodeData = null;
                WindowedDataTransmissionData? dataTransmissionData = null;

                if (isFinal)
                {
                    // For final snapshot, get current data without resetting
                    durationData = DurationAggregator?.GetCurrentWindowData();
                    throughputData = ThroughputAggregator?.GetCurrentWindowData();
                    responseCodeData = ResponseCodeAggregator?.GetCurrentWindowData();
                    dataTransmissionData = DataTransmissionAggregator?.GetCurrentWindowData();
                }
                else
                {
                    // For regular windows, get and reset
                    durationData = DurationAggregator?.GetWindowDataAndReset();
                    throughputData = ThroughputAggregator?.GetWindowDataAndReset();
                    responseCodeData = ResponseCodeAggregator?.GetWindowDataAndReset();
                    dataTransmissionData = DataTransmissionAggregator?.GetWindowDataAndReset();
                }

                // Build snapshot with windowed data only
                // Cumulative data is handled separately by CumulativeIterationMetricsCollector
                var snapshot = new WindowedIterationSnapshot
                {
                    IterationId = _httpIteration.Id,
                    PlanName = _planContext.PlanName,
                    TestStartTime = _planContext.TestStartTime,
                    RoundName = _roundName,
                    IterationName = _httpIteration.Name,
                    TargetUrl = _httpIteration.HttpRequest?.Url?.BaseUrl ?? string.Empty,
                    WindowSequence = windowSequence,
                    WindowStart = _windowStart,
                    WindowEnd = windowEnd,
                    ExecutionStatus = executionStatus,
                    IsFinal = isFinal,
                    
                    // Windowed (for charts)
                    Duration = durationData,
                    Throughput = throughputData,
                    ResponseCodes = responseCodeData,
                    DataTransmission = dataTransmissionData
                };

                // For final snapshot, always push regardless of data; for regular, only push if there's data
                if (isFinal || snapshot.HasData)
                {
                    _queue.TryEnqueue(snapshot);
                }

                if (!isFinal)
                {
                    _windowStart = windowEnd;
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
            _coordinator.OnWindowClosed -= OnWindowClosed;
            _semaphore.Dispose();
        }
    }
}
