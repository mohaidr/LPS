using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IWarmUpService : IDisposable
{
    /// <summary>
    /// Warms up the given hosts by issuing lightweight GET requests.
    /// </summary>
    /// <param name="hosts">Absolute host URLs (e.g., "https://api.example.com").</param>
    /// <param name="requestsPerHost">Number of requests to send per host (default 3).</param>
    /// <param name="path">Relative path to request on each host (default "/").</param>
    /// <param name="timeout">Per-request timeout (default 30s if null).</param>
    /// <param name="ct">Cancellation token.</param>
   public Task<bool> TryWarmUpAsync(
        IEnumerable<string> hosts,
        int requestsPerHost = 3,
        string path = "/",
        TimeSpan? timeout = null,
        CancellationToken ct = default);
}