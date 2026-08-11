#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.Cumulative;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostCumulativeDataTransmissionAggregator : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;
        private double _dataSent;
        private double _dataReceived;

        public async ValueTask UpdateDataSentAsync(double totalBytes, CancellationToken token)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                _dataSent += totalBytes;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask UpdateDataReceivedAsync(double totalBytes, CancellationToken token)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                _dataReceived += totalBytes;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public CumulativeDataTransmissionData GetCumulativeData(double elapsedSeconds, long requestsCount)
        {
            _semaphore.Wait();
            try
            {
                ThrowIfDisposed();
                return new CumulativeDataTransmissionData
                {
                    DataSent = _dataSent,
                    DataReceived = _dataReceived,
                    AverageDataSent = requestsCount > 0 ? _dataSent / requestsCount : 0,
                    AverageDataReceived = requestsCount > 0 ? _dataReceived / requestsCount : 0,
                    UpstreamThroughputBps = _dataSent / elapsedSeconds,
                    DownstreamThroughputBps = _dataReceived / elapsedSeconds,
                    ThroughputBps = (_dataSent + _dataReceived) / elapsedSeconds
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _semaphore.Dispose();
        }
    }
}
