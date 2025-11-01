using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public sealed class WarmUpService : IWarmUpService
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly ILogger _logger;

    public WarmUpService(ILogger logger, HttpClient? client = null)
    {
        _client = client ?? new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            EnableMultipleHttp2Connections = true
        });
        _logger = logger;
        _ownsClient = client is null;
    }

    /// <summary>
    /// Tries to warm up the given hosts. Returns false if any exception occurs (logged), true otherwise.
    /// </summary>
    public async Task<bool> TryWarmUpAsync(
        IEnumerable<string> hosts,
        int requestsPerHost = 3,
        string path = "/",
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        if (requestsPerHost <= 0) return true;

        var t = timeout ?? TimeSpan.FromSeconds(30);
        var hadAnyException = false;

        foreach (var host in hosts)
        {
            Uri baseUri;
            try
            {
                baseUri = new Uri(host, UriKind.Absolute);
            }
            catch (Exception ex)
            {
                hadAnyException = true;
                await _logger.LogAsync(
                    $"WarmUp invalid host '{host}': {ex}",
                    LPSLoggingLevel.Error,
                    ct);
                continue;
            }

            for (int i = 0; i < requestsPerHost; i++)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(t);

                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path))
                    {
                        Version = HttpVersion.Version20,
                        VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                    };

                    using var res = await _client.SendAsync(
                        req,
                        HttpCompletionOption.ResponseHeadersRead,
                        cts.Token);

                    // Note: not throwing on non-success; only exceptions flip the return to false.
                    await _logger.LogAsync(
                        $"WarmUp request to {host}{path} completed with {(int)res.StatusCode} {res.StatusCode}",
                        LPSLoggingLevel.Verbose,
                        ct);
                }
                catch (OperationCanceledException oce) when (cts.IsCancellationRequested)
                {
                    hadAnyException = true;
                    await _logger.LogAsync(
                        $"WarmUp request to {host}{path} timed out after {t}. Exception: {oce}",
                        LPSLoggingLevel.Error,
                        ct);
                }
                catch (Exception ex)
                {
                    hadAnyException = true;
                    await _logger.LogAsync(
                        $"WarmUp request to {host}{path} failed: {ex}",
                        LPSLoggingLevel.Error,
                        ct);
                }
            }
        }

        return !hadAnyException;
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }
}
