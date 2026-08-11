#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostWindowedDurationAggregator : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private Dictionary<DurationMetricType, HostTimingAccumulator> _durations = new();
        private bool _disposed;

        public async ValueTask UpdateAsync(DurationMetricType metricType, double valueMs, CancellationToken token)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                if (!_durations.TryGetValue(metricType, out var accumulator))
                {
                    accumulator = new HostTimingAccumulator();
                    _durations[metricType] = accumulator;
                }

                accumulator.Record(valueMs);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public WindowedDurationData GetWindowDataAndReset()
        {
            _semaphore.Wait();
            try
            {
                ThrowIfDisposed();
                var data = new WindowedDurationData
                {
                    TotalTime = GetTiming(DurationMetricType.TotalTime),
                    TCPHandshakeTime = GetTiming(DurationMetricType.TCPHandshakeTime),
                    SSLHandshakeTime = GetTiming(DurationMetricType.TLSHandshakeTime),
                    TimeToFirstByte = GetTiming(DurationMetricType.TimeToFirstByte),
                    WaitingTime = GetTiming(DurationMetricType.WaitingTime),
                    ReceivingTime = GetTiming(DurationMetricType.ReceivingTime),
                    SendingTime = GetTiming(DurationMetricType.SendingTime),
                    ServerTime = GetTiming(DurationMetricType.ServerTime),
                    ServerTimeDB = GetTiming(DurationMetricType.ServerTimeDB),
                    ServerTimeCache = GetTiming(DurationMetricType.ServerTimeCache),
                    ServerTimeApp = GetTiming(DurationMetricType.ServerTimeApp)
                };

                _durations = new Dictionary<DurationMetricType, HostTimingAccumulator>();
                return data;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private WindowedTimingMetric GetTiming(DurationMetricType metricType) =>
            _durations.TryGetValue(metricType, out var accumulator)
                ? accumulator.ToWindowedMetric()
                : new WindowedTimingMetric();

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _semaphore.Dispose();
        }
    }
}
