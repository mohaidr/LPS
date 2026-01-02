#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using HdrHistogram;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.Windowed
{
    /// <summary>
    /// Windowed duration/timing aggregator. Collects timing metrics within a window.
    /// The WindowedIterationMetricsCollector calls GetWindowDataAndReset() on window close.
    /// </summary>
    public sealed class WindowedDurationAggregator : IMetricAggregator, IDurationMetricCollector, IDisposable
    {
        private readonly HttpIteration _httpIteration;
        private readonly string _roundName;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;

        // Timing metrics (resettable)
        private WindowedTimingAccumulator _totalTime = new();
        private WindowedTimingAccumulator _tcpHandshakeTime = new();
        private WindowedTimingAccumulator _sslHandshakeTime = new();
        private WindowedTimingAccumulator _timeToFirstByte = new();
        private WindowedTimingAccumulator _waitingTime = new();
        private WindowedTimingAccumulator _receivingTime = new();
        private WindowedTimingAccumulator _sendingTime = new();

        public HttpIteration HttpIteration => _httpIteration;
        public LPSMetricType MetricType => LPSMetricType.Time;

        public WindowedDurationAggregator(
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
        public WindowedDurationData? GetWindowDataAndReset()
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
        /// Returns current window data without resetting.
        /// Used for final snapshot when iteration ends.
        /// </summary>
        public WindowedDurationData? GetCurrentWindowData()
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

        private WindowedDurationData BuildWindowData()
        {
            return new WindowedDurationData
            {
                TotalTime = _totalTime.ToMetric(),
                TCPHandshakeTime = _tcpHandshakeTime.ToMetric(),
                SSLHandshakeTime = _sslHandshakeTime.ToMetric(),
                TimeToFirstByte = _timeToFirstByte.ToMetric(),
                WaitingTime = _waitingTime.ToMetric(),
                ReceivingTime = _receivingTime.ToMetric(),
                SendingTime = _sendingTime.ToMetric()
            };
        }

        private void Reset()
        {
            _totalTime = new WindowedTimingAccumulator();
            _tcpHandshakeTime = new WindowedTimingAccumulator();
            _sslHandshakeTime = new WindowedTimingAccumulator();
            _timeToFirstByte = new WindowedTimingAccumulator();
            _waitingTime = new WindowedTimingAccumulator();
            _receivingTime = new WindowedTimingAccumulator();
            _sendingTime = new WindowedTimingAccumulator();
        }

        #region IDurationMetricCollector Implementation

        public async Task<IDurationMetricCollector> UpdateTotalTimeAsync(double totalTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try { _totalTime.Record(totalTime); }
            finally { _semaphore.Release(); }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateReceivingTimeAsync(double receivingTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try { _receivingTime.Record(receivingTime); }
            finally { _semaphore.Release(); }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateSendingTimeAsync(double sendingTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try { _sendingTime.Record(sendingTime); }
            finally { _semaphore.Release(); }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateTLSHandshakeTimeAsync(double tlsHandshakeTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try { _sslHandshakeTime.Record(tlsHandshakeTime); }
            finally { _semaphore.Release(); }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateTCPHandshakeTimeAsync(double tcpHandshakeTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try { _tcpHandshakeTime.Record(tcpHandshakeTime); }
            finally { _semaphore.Release(); }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateTimeToFirstByteAsync(double timeToFirstByte, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try { _timeToFirstByte.Record(timeToFirstByte); }
            finally { _semaphore.Release(); }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateWaitingTimeAsync(double waitingTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try { _waitingTime.Record(waitingTime); }
            finally { _semaphore.Release(); }
            return this;
        }

        #endregion

        #region IMetricAggregator Implementation

        public string Stringify() => $"WindowedDuration[{_roundName}/{_httpIteration.Name}]";

        public ValueTask<IMetricShapshot> GetSnapshotAsync(CancellationToken token)
        {
            // Not used for windowed - data goes to queue
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

    /// <summary>
    /// Accumulator for a single timing metric with HdrHistogram.
    /// </summary>
    internal sealed class WindowedTimingAccumulator
    {
        private readonly LongHistogram _histogram = new(1, 1000000, 3);
        private int _count;
        private double _sum;
        private double _min = double.MaxValue;
        private double _max;
        private bool _hasNonZeroValues;

        public void Record(double valueMs)
        {
            _count++;
            _sum += valueMs;
            _min = Math.Min(_min, valueMs);
            _max = Math.Max(_max, valueMs);

            // Only record to histogram if value is meaningful (> 0)
            // This prevents 0-value metrics from skewing percentiles
            if (valueMs > 0)
            {
                _hasNonZeroValues = true;
                long histValue = Math.Min((long)Math.Ceiling(valueMs), 1000000);
                histValue = Math.Max(1, histValue); // Histogram requires minimum 1
                _histogram.RecordValue(histValue);
            }
        }

        public WindowedTimingMetric ToMetric()
        {
            // If no non-zero values were recorded, return 0 for percentiles
            var hasData = _hasNonZeroValues && _histogram.TotalCount > 0;
            
            return new WindowedTimingMetric
            {
                Count = _count,
                Sum = _sum,
                Average = _count > 0 ? _sum / _count : 0,
                Min = _min == double.MaxValue ? 0 : _min,
                Max = _max,
                P50 = hasData ? _histogram.GetValueAtPercentile(50) : 0,
                P90 = hasData ? _histogram.GetValueAtPercentile(90) : 0,
                P95 = hasData ? _histogram.GetValueAtPercentile(95) : 0,
                P99 = hasData ? _histogram.GetValueAtPercentile(99) : 0
            };
        }
    }
}
