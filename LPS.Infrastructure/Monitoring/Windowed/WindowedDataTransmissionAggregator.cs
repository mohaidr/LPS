#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.Windowed
{
    /// <summary>
    /// Windowed data transmission aggregator. Tracks bytes sent/received within a window.
    /// The WindowedIterationMetricsCollector calls GetWindowDataAndReset() on window close.
    /// </summary>
    public sealed class WindowedDataTransmissionAggregator : IMetricAggregator, IDataTransmissionMetricAggregator, IDisposable
    {
        private readonly HttpIteration _httpIteration;
        private readonly string _roundName;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;
        private DateTime _windowStart = DateTime.UtcNow;

        // Transmission counters (resettable)
        private double _dataSent;
        private double _dataReceived;

        public HttpIteration HttpIteration => _httpIteration;
        public LPSMetricType MetricType => LPSMetricType.DataTransmission;

        public WindowedDataTransmissionAggregator(
            HttpIteration httpIteration,
            string roundName)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));
        }

        /// <summary>
        /// Called by the WindowedIterationMetricsCollector on window close.
        /// Returns current window data and resets for next window.
        /// </summary>
        public WindowedDataTransmissionData? GetWindowDataAndReset()
        {
            _semaphore.Wait();
            try
            {
                if (_disposed) return null;
                var data = BuildWindowData();
                Reset();
                return data.HasData ? data : null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Called by the WindowedIterationMetricsCollector for final snapshot.
        /// Returns current window data WITHOUT resetting (for final push on iteration end).
        /// </summary>
        public WindowedDataTransmissionData? GetCurrentWindowData()
        {
            _semaphore.Wait();
            try
            {
                if (_disposed) return null;
                var data = BuildWindowData();
                return data.HasData ? data : null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private WindowedDataTransmissionData BuildWindowData()
        {
            var windowEnd = DateTime.UtcNow;
            var windowDuration = (windowEnd - _windowStart).TotalSeconds;
            if (windowDuration < 0.001) windowDuration = 0.001;

            return new WindowedDataTransmissionData
            {
                DataSent = _dataSent,
                DataReceived = _dataReceived,
                UpstreamThroughputBps = _dataSent / windowDuration,
                DownstreamThroughputBps = _dataReceived / windowDuration,
                ThroughputBps = (_dataSent + _dataReceived) / windowDuration
            };
        }

        private void Reset()
        {
            _dataSent = 0;
            _dataReceived = 0;
            _windowStart = DateTime.UtcNow;
        }

        #region IDataTransmissionMetricAggregator Implementation

        public async ValueTask UpdateDataSentAsync(double dataSize, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try { _dataSent += dataSize; }
            finally { _semaphore.Release(); }
        }

        public async ValueTask UpdateDataReceivedAsync(double dataSize, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try { _dataReceived += dataSize; }
            finally { _semaphore.Release(); }
        }

        #endregion

        #region IMetricAggregator Implementation

        public string Stringify() => $"WindowedDataTransmission[{_roundName}/{_httpIteration.Name}]";

        public ValueTask<IMetricShapshot> GetSnapshotAsync(CancellationToken token)
        {
            throw new NotSupportedException("Windowed aggregators push to queue, not via GetSnapshotAsync");
        }

        public ValueTask<TDimensionSet> GetSnapshotAsync<TDimensionSet>(CancellationToken token) where TDimensionSet : IMetricShapshot
        {
            throw new NotSupportedException("Windowed aggregators push to queue, not via GetSnapshotAsync");
        }

        public ValueTask StartAsync(CancellationToken token) => ValueTask.CompletedTask;

        public ValueTask StopAsync(CancellationToken token) => ValueTask.CompletedTask;

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _semaphore.Dispose();
        }
    }
}
