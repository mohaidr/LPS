#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostMetricsAggregator : IHostMetricsAggregator, IDisposable
    {
        private readonly SemaphoreSlim _windowSnapshotSemaphore = new(1, 1);

        private readonly HostCumulativeThroughputAggregator _cumulativeThroughput = new();
        private readonly HostCumulativeDurationAggregator _cumulativeDuration = new();
        private readonly HostCumulativeResponseCodeAggregator _cumulativeResponseCodes = new();
        private readonly HostCumulativeDataTransmissionAggregator _cumulativeDataTransmission = new();

        private readonly HostWindowedThroughputAggregator _windowedThroughput = new();
        private readonly HostWindowedDurationAggregator _windowedDuration = new();
        private readonly HostWindowedResponseCodeAggregator _windowedResponseCodes = new();
        private readonly HostWindowedDataTransmissionAggregator _windowedDataTransmission = new();

        private readonly DateTime _startedAt = DateTime.UtcNow;
        private DateTime _windowStartedAt = DateTime.UtcNow;
        private bool _disposed;

        public HostMetricsAggregator(HostKey hostKey)
        {
            HostKey = hostKey;
        }

        public HostKey HostKey { get; }

        public async ValueTask IncreaseConnectionsCountAsync(CancellationToken token)
        {
            ThrowIfDisposed();
            await _cumulativeThroughput.IncreaseConnectionsCountAsync(token).ConfigureAwait(false);
            await _windowedThroughput.IncreaseConnectionsCountAsync(token).ConfigureAwait(false);
        }

        public async ValueTask DecreaseConnectionsCountAsync(CancellationToken token)
        {
            ThrowIfDisposed();
            await _cumulativeThroughput.DecreaseConnectionsCountAsync(token).ConfigureAwait(false);
            await _windowedThroughput.DecreaseConnectionsCountAsync(token).ConfigureAwait(false);
        }

        public async ValueTask IncreaseSkippedRequestsCountAsync(CancellationToken token)
        {
            ThrowIfDisposed();
            await _cumulativeThroughput.IncreaseSkippedRequestsCountAsync(token).ConfigureAwait(false);
            await _windowedThroughput.IncreaseSkippedRequestsCountAsync(token).ConfigureAwait(false);
        }

        public async ValueTask UpdateResponseAsync(HttpResponse.SetupCommand response, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(response);
            ThrowIfDisposed();

            await _cumulativeResponseCodes.UpdateAsync(response, token).ConfigureAwait(false);
            await _windowedResponseCodes.UpdateAsync(response, token).ConfigureAwait(false);
            await _cumulativeThroughput.UpdateResponseOutcomeAsync(response.IsSuccessStatusCode, token).ConfigureAwait(false);
            await _windowedThroughput.UpdateResponseOutcomeAsync(response.IsSuccessStatusCode, token).ConfigureAwait(false);
        }

        public async ValueTask UpdateDurationAsync(DurationMetricType metricType, double valueMs, CancellationToken token)
        {
            ThrowIfDisposed();
            await _cumulativeDuration.UpdateAsync(metricType, valueMs, token).ConfigureAwait(false);
            await _windowedDuration.UpdateAsync(metricType, valueMs, token).ConfigureAwait(false);
        }

        public async ValueTask UpdateDataSentAsync(double totalBytes, CancellationToken token)
        {
            ThrowIfDisposed();
            await _cumulativeDataTransmission.UpdateDataSentAsync(totalBytes, token).ConfigureAwait(false);
            await _windowedDataTransmission.UpdateDataSentAsync(totalBytes, token).ConfigureAwait(false);
        }

        public async ValueTask UpdateDataReceivedAsync(double totalBytes, CancellationToken token)
        {
            ThrowIfDisposed();
            await _cumulativeDataTransmission.UpdateDataReceivedAsync(totalBytes, token).ConfigureAwait(false);
            await _windowedDataTransmission.UpdateDataReceivedAsync(totalBytes, token).ConfigureAwait(false);
        }

        public HostCumulativeMetricsSnapshot GetCumulativeSnapshot()
        {
            ThrowIfDisposed();
            var timestamp = DateTime.UtcNow;
            var elapsedSeconds = Math.Max((timestamp - _startedAt).TotalSeconds, 0.001);
            var throughput = _cumulativeThroughput.GetCumulativeData(elapsedSeconds);

            return new HostCumulativeMetricsSnapshot
            {
                HostKey = HostKey,
                Timestamp = timestamp,
                Throughput = throughput,
                Duration = _cumulativeDuration.GetCumulativeData(),
                DataTransmission = _cumulativeDataTransmission.GetCumulativeData(elapsedSeconds, throughput.RequestsCount),
                ResponseCodes = _cumulativeResponseCodes.GetCumulativeData()
            };
        }

        public HostWindowedMetricsSnapshot GetWindowedSnapshotAndReset()
        {
            _windowSnapshotSemaphore.Wait();
            try
            {
                ThrowIfDisposed();
                var windowEnd = DateTime.UtcNow;
                var elapsedSeconds = Math.Max((windowEnd - _windowStartedAt).TotalSeconds, 0.001);
                var snapshot = new HostWindowedMetricsSnapshot
                {
                    HostKey = HostKey,
                    WindowStart = _windowStartedAt,
                    WindowEnd = windowEnd,
                    Throughput = _windowedThroughput.GetWindowDataAndReset(elapsedSeconds),
                    Duration = _windowedDuration.GetWindowDataAndReset(),
                    DataTransmission = _windowedDataTransmission.GetWindowDataAndReset(elapsedSeconds),
                    ResponseCodes = _windowedResponseCodes.GetWindowDataAndReset()
                };

                _windowStartedAt = windowEnd;
                return snapshot;
            }
            finally
            {
                _windowSnapshotSemaphore.Release();
            }
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cumulativeThroughput.Dispose();
            _cumulativeDuration.Dispose();
            _cumulativeResponseCodes.Dispose();
            _cumulativeDataTransmission.Dispose();
            _windowedThroughput.Dispose();
            _windowedDuration.Dispose();
            _windowedResponseCodes.Dispose();
            _windowedDataTransmission.Dispose();
            _windowSnapshotSemaphore.Dispose();
        }
    }
}