#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostCumulativeDurationAggregator : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly Dictionary<DurationMetricType, HostTimingAccumulator> _durations = new();
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

        public CumulativeDurationData GetCumulativeData()
        {
            _semaphore.Wait();
            try
            {
                ThrowIfDisposed();
                return new CumulativeDurationData
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
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private CumulativeTimingMetric GetTiming(DurationMetricType metricType) =>
            _durations.TryGetValue(metricType, out var accumulator)
                ? accumulator.ToCumulativeMetric()
                : new CumulativeTimingMetric();

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _semaphore.Dispose();
        }
    }
}
