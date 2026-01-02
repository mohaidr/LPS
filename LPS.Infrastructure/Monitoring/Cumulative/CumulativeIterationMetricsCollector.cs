#nullable enable
using System;
using System.Linq;
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
    /// collects cumulative data from the metric data store, and pushes to queue.
    /// Self-manages lifecycle based on iteration status from IIterationStatusMonitor.
    /// </summary>
    public sealed class CumulativeIterationMetricsCollector : IDisposable
    {
        private readonly HttpIteration _httpIteration;
        private readonly string _roundName;
        private readonly ICumulativeMetricsQueue _queue;
        private readonly ICumulativeMetricsCoordinator _coordinator;
        private readonly IMetricDataStore _metricDataStore;
        private readonly IIterationStatusMonitor _iterationStatusMonitor;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private bool _disposed;
        private bool _finalSnapshotSent;

        public HttpIteration HttpIteration => _httpIteration;
        
        public CumulativeIterationMetricsCollector(
            HttpIteration httpIteration,
            string roundName,
            ICumulativeMetricsQueue queue,
            ICumulativeMetricsCoordinator coordinator,
            IMetricDataStore metricDataStore,
            IIterationStatusMonitor iterationStatusMonitor)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _metricDataStore = metricDataStore ?? throw new ArgumentNullException(nameof(metricDataStore));
            _iterationStatusMonitor = iterationStatusMonitor ?? throw new ArgumentNullException(nameof(iterationStatusMonitor));

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
                // Collect cumulative data from the data store
                var throughput = GetCumulativeThroughput(out var targetUrl);
                var duration = GetCumulativeDuration();
                var dataTransmission = GetCumulativeDataTransmission();
                var responseCodes = GetCumulativeResponseCodes();

                var snapshot = new CumulativeIterationSnapshot
                {
                    IterationId = _httpIteration.Id,
                    RoundName = _roundName,
                    IterationName = _httpIteration.Name,
                    TargetUrl = targetUrl ?? string.Empty,
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
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private CumulativeThroughputData? GetCumulativeThroughput(out string? targetUrl)
        {
            targetUrl = null;
            if (!_metricDataStore.TryGetLatest<ThroughputMetricSnapshot>(
                _httpIteration.Id, LPSMetricType.Throughput, out var snapshot) || snapshot == null)
            {
                return null;
            }

            targetUrl = snapshot.URL;
            return new CumulativeThroughputData
            {
                RequestsCount = snapshot.RequestsCount,
                SuccessfulRequestCount = snapshot.SuccessfulRequestCount,
                FailedRequestsCount = snapshot.FailedRequestsCount,
                ActiveRequestsCount = snapshot.ActiveRequestsCount,
                RequestsPerSecond = snapshot.RequestsRate.Value,
                ErrorRate = snapshot.ErrorRate,
                TimeElapsedMs = snapshot.TimeElapsed
            };
        }

        private CumulativeDurationData? GetCumulativeDuration()
        {
            if (!_metricDataStore.TryGetLatest<DurationMetricSnapshot>(
                _httpIteration.Id, LPSMetricType.Time, out var snapshot) || snapshot == null)
            {
                return null;
            }

            return new CumulativeDurationData
            {
                TotalTime = MapTimingMetric(snapshot.TotalTimeMetrics),
                TCPHandshakeTime = MapTimingMetric(snapshot.TCPHandshakeTimeMetrics),
                SSLHandshakeTime = MapTimingMetric(snapshot.SSLHandshakeTimeMetrics),
                TimeToFirstByte = MapTimingMetric(snapshot.TimeToFirstByteMetrics),
                WaitingTime = MapTimingMetric(snapshot.WaitingTimeMetrics),
                ReceivingTime = MapTimingMetric(snapshot.ReceivingTimeMetrics),
                SendingTime = MapTimingMetric(snapshot.SendingTimeMetrics)
            };
        }

        private static CumulativeTimingMetric MapTimingMetric(DurationMetricSnapshot.MetricTime metric)
        {
            return new CumulativeTimingMetric
            {
                Sum = metric.Sum,
                Average = metric.Average,
                Min = metric.Min,
                Max = metric.Max,
                P50 = metric.P50,
                P90 = metric.P90,
                P95 = metric.P95,
                P99 = metric.P99
            };
        }

        private CumulativeDataTransmissionData? GetCumulativeDataTransmission()
        {
            if (!_metricDataStore.TryGetLatest<DataTransmissionMetricSnapshot>(
                _httpIteration.Id, LPSMetricType.DataTransmission, out var snapshot) || snapshot == null)
            {
                return null;
            }

            return new CumulativeDataTransmissionData
            {
                DataSent = snapshot.DataSent,
                DataReceived = snapshot.DataReceived,
                AverageDataSent = snapshot.AverageDataSent,
                AverageDataReceived = snapshot.AverageDataReceived,
                UpstreamThroughputBps = snapshot.UpstreamThroughputBps,
                DownstreamThroughputBps = snapshot.DownstreamThroughputBps,
                ThroughputBps = snapshot.ThroughputBps
            };
        }

        private CumulativeResponseCodeData? GetCumulativeResponseCodes()
        {
            if (!_metricDataStore.TryGetLatest<ResponseCodeMetricSnapshot>(
                _httpIteration.Id, LPSMetricType.ResponseCode, out var snapshot) || snapshot == null)
            {
                return null;
            }

            return new CumulativeResponseCodeData
            {
                ResponseSummaries = snapshot.ResponseSummaries
                    .Select(s => new CumulativeResponseSummary
                    {
                        HttpStatusCode = (int)s.HttpStatusCode,
                        HttpStatusReason = s.HttpStatusReason,
                        Count = s.Count
                    })
                    .ToList()
            };
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
