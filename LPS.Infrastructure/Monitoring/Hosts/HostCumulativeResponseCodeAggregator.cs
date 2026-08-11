#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Infrastructure.Monitoring.Cumulative;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostCumulativeResponseCodeAggregator : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly Dictionary<int, (string Reason, long Count)> _responseCodes = new();
        private bool _disposed;

        public async ValueTask UpdateAsync(HttpResponse.SetupCommand response, CancellationToken token)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                var statusCode = (int)response.StatusCode;
                var reason = response.StatusMessage ?? response.StatusCode.ToString();
                var existing = _responseCodes.GetValueOrDefault(statusCode);
                _responseCodes[statusCode] = (existing.Reason ?? reason, existing.Count + 1);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public CumulativeResponseCodeData GetCumulativeData()
        {
            _semaphore.Wait();
            try
            {
                ThrowIfDisposed();
                var summaries = new List<CumulativeResponseSummary>(_responseCodes.Count);
                foreach (var (statusCode, value) in _responseCodes)
                {
                    summaries.Add(new CumulativeResponseSummary
                    {
                        HttpStatusCode = statusCode,
                        HttpStatusReason = value.Reason,
                        Count = value.Count
                    });
                }

                return new CumulativeResponseCodeData { ResponseSummaries = summaries };
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
