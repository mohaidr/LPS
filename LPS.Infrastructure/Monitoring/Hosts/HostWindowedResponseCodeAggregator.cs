#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostWindowedResponseCodeAggregator : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private Dictionary<int, (string Reason, int Count)> _responseCodes = new();
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

        public WindowedResponseCodeData GetWindowDataAndReset()
        {
            _semaphore.Wait();
            try
            {
                ThrowIfDisposed();
                var summaries = new List<WindowedResponseSummary>(_responseCodes.Count);
                foreach (var (statusCode, value) in _responseCodes)
                {
                    summaries.Add(new WindowedResponseSummary
                    {
                        HttpStatusCode = (System.Net.HttpStatusCode)statusCode,
                        HttpStatusReason = value.Reason,
                        Count = value.Count
                    });
                }

                _responseCodes = new Dictionary<int, (string Reason, int Count)>();
                return new WindowedResponseCodeData { ResponseSummaries = summaries };
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
