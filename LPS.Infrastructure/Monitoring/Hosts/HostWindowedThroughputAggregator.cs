#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostWindowedThroughputAggregator : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;
        private int _requestsCount;
        private int _skippedRequestsCount;
        private int _successfulRequestsCount;
        private int _failedRequestsCount;
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

        public async ValueTask UpdateResponseOutcomeAsync(bool isSuccessful, CancellationToken token)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                if (isSuccessful)
                    _successfulRequestsCount++;
                else
                    _failedRequestsCount++;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public WindowedThroughputData GetWindowDataAndReset(double elapsedSeconds)
        {
            _semaphore.Wait();
            try
            {
                ThrowIfDisposed();
                var data = new WindowedThroughputData
                {
                    RequestsCount = _requestsCount,
                    SkippedRequestsCount = _skippedRequestsCount,
                    SuccessfulRequestCount = _successfulRequestsCount,
                    FailedRequestsCount = _failedRequestsCount,
                    MaxConcurrentRequests = _maxConcurrentRequests,
                    RequestsPerSecond = _requestsCount / elapsedSeconds,
                    ErrorRate = _requestsCount > 0
                        ? (double)_failedRequestsCount / _requestsCount * 100
                        : 0
                };

                _requestsCount = 0;
                _skippedRequestsCount = 0;
                _successfulRequestsCount = 0;
                _failedRequestsCount = 0;
                _maxConcurrentRequests = _activeRequests;
                return data;
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
