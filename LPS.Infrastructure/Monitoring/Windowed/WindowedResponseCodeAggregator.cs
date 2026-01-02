#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.Windowed
{
    /// <summary>
    /// Windowed response code aggregator. Tracks HTTP status codes within a window.
    /// The WindowedIterationMetricsCollector calls GetWindowDataAndReset() on window close.
    /// </summary>
    public sealed class WindowedResponseCodeAggregator : IMetricAggregator, IResponseMetricCollector, IDisposable
    {
        private readonly HttpIteration _httpIteration;
        private readonly string _roundName;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;

        // Response code counters (resettable)
        private Dictionary<HttpStatusCode, (string Reason, int Count)> _statusCodes = new();

        public HttpIteration HttpIteration => _httpIteration;
        public LPSMetricType MetricType => LPSMetricType.ResponseCode;

        public WindowedResponseCodeAggregator(
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
        public WindowedResponseCodeData? GetWindowDataAndReset()
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
        public WindowedResponseCodeData? GetCurrentWindowData()
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

        private WindowedResponseCodeData BuildWindowData()
        {
            var responseSummaries = new List<WindowedResponseSummary>();
            foreach (var kvp in _statusCodes)
            {
                responseSummaries.Add(new WindowedResponseSummary
                {
                    HttpStatusCode = kvp.Key,
                    HttpStatusReason = kvp.Value.Reason,
                    Count = kvp.Value.Count
                });
            }

            return new WindowedResponseCodeData
            {
                ResponseSummaries = responseSummaries
            };
        }

        private void Reset()
        {
            _statusCodes = new Dictionary<HttpStatusCode, (string Reason, int Count)>();
        }

        #region IResponseMetricCollector Implementation

        public IResponseMetricCollector Update(HttpResponse.SetupCommand response, CancellationToken token)
        {
            UpdateAsync(response, token).Wait();
            return this;
        }

        public async Task<IResponseMetricCollector> UpdateAsync(HttpResponse.SetupCommand response, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                var statusCode = response.StatusCode;
                var statusReason = response.StatusMessage ?? statusCode.ToString();
                if (!_statusCodes.TryGetValue(statusCode, out var existing))
                {
                    _statusCodes[statusCode] = (statusReason, 1);
                }
                else
                {
                    _statusCodes[statusCode] = (existing.Reason, existing.Count + 1);
                }
            }
            finally { _semaphore.Release(); }
            return this;
        }

        #endregion

        #region IMetricAggregator Implementation

        public string Stringify() => $"WindowedResponseCode[{_roundName}/{_httpIteration.Name}]";

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
