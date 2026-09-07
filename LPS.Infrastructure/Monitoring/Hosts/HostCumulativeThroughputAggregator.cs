#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.Cumulative;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostCumulativeThroughputAggregator : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;
        private long _requestsCount;
        private long _skippedRequestsCount;
        private int _activeRequests;
        private int _maxConcurrentRequests;

        public async ValueTask IncreaseConnectionsCountAsync(CancellationToken token)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                _requestsCount++;
                _activeRequests++;
                _maxConcurrentRequests = Math.Max(_maxConcurrentRequests, _activeRequests);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask DecreaseConnectionsCountAsync(CancellationToken token)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                _activeRequests = Math.Max(0, _activeRequests - 1);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask IncreaseSkippedRequestsCountAsync(CancellationToken token)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                _skippedRequestsCount++;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        // Success/failure are folded from the host's iterations (rule-based); the host itself never classifies responses.
        public CumulativeThroughputData GetCumulativeData(double elapsedSeconds, long successful, long failed)
        {
            _semaphore.Wait();
            try
            {
                ThrowIfDisposed();
                return new CumulativeThroughputData
                {
                    RequestsCount = _requestsCount,
                    SkippedRequestsCount = _skippedRequestsCount,
                    SuccessfulRequestCount = successful,
                    FailedRequestsCount = failed,
                    MaxConcurrentRequests = _maxConcurrentRequests,
                    RequestsPerSecond = successful / elapsedSeconds,
                    ErrorRate = _requestsCount > 0 ? (double)failed / _requestsCount * 100 : 0,
                    TimeElapsedMs = elapsedSeconds * 1000
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
