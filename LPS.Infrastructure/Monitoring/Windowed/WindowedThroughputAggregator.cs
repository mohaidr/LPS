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
    /// Windowed throughput aggregator. Tracks request counts and errors within a window.
    /// The WindowedIterationMetricsCollector calls GetWindowDataAndReset() on window close.
    /// </summary>
    public sealed class WindowedThroughputAggregator : IMetricAggregator, IThroughputMetricCollector, IDisposable
    {
        private readonly HttpIteration _httpIteration;
        private readonly string _roundName;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;
        private DateTime _windowStart = DateTime.UtcNow;

        // Throughput counters (resettable per window)
        private long _requestsCount;
        private long _successfulRequests;
        private long _failedRequests;
        private int _currentActiveRequests;  // Current in-flight requests (not reset)
        private int _maxConcurrentRequests;  // Peak concurrent within this window (reset per window)

        public HttpIteration HttpIteration => _httpIteration;
        public LPSMetricType MetricType => LPSMetricType.Throughput;

        public WindowedThroughputAggregator(
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
        public WindowedThroughputData? GetWindowDataAndReset()
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
        public WindowedThroughputData? GetCurrentWindowData()
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

        private WindowedThroughputData BuildWindowData()
        {
            var windowEnd = DateTime.UtcNow;
            var windowDuration = (windowEnd - _windowStart).TotalSeconds;
            var requestsPerSecond = windowDuration > 0 ? _requestsCount / windowDuration : 0;
            var errorRate = _requestsCount > 0 ? (double)_failedRequests / _requestsCount * 100 : 0;

            return new WindowedThroughputData
            {
                RequestsCount = (int)_requestsCount,
                SuccessfulRequestCount = (int)_successfulRequests,
                FailedRequestsCount = (int)_failedRequests,
                MaxConcurrentRequests = _maxConcurrentRequests,
                RequestsPerSecond = requestsPerSecond,
                ErrorRate = errorRate
            };
        }

        /// <summary>
        /// Updates success/failure counts based on response codes.
        /// Called when response data is available.
        /// </summary>
        /// <summary>
        /// Updates success/failure counts. Called by WindowedResponseCodeAggregator.
        /// </summary>
        public void UpdateSuccessFailure(int successCount, int failedCount)
        {
            _semaphore.Wait();
            try
            {
                _successfulRequests = successCount;
                _failedRequests = failedCount;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void Reset()
        {
            _requestsCount = 0;
            _successfulRequests = 0;
            _failedRequests = 0;
            // New window starts with max = current in-flight (those requests are still concurrent)
            _maxConcurrentRequests = _currentActiveRequests;
            _windowStart = DateTime.UtcNow;
        }

        #region IThroughputMetricCollector Implementation

        public async ValueTask<bool> IncreaseConnectionsCount(CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try 
            { 
                _requestsCount++;
                _currentActiveRequests++;
                
                // Track the peak concurrent requests within this window
                if (_currentActiveRequests > _maxConcurrentRequests)
                {
                    _maxConcurrentRequests = _currentActiveRequests;
                }
            }
            finally { _semaphore.Release(); }
            return true;
        }

        public async ValueTask<bool> DecreseConnectionsCount(CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try 
            { 
                _currentActiveRequests--;
            }
            finally { _semaphore.Release(); }
            return true;
        }

        #endregion

        #region IMetricAggregator Implementation

        public string Stringify() => $"WindowedThroughput[{_roundName}/{_httpIteration.Name}]";

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
