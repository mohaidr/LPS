#nullable enable
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;
using Microsoft.Extensions.Logging;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    /// <summary>
    /// Writes LPS metrics to customer's InfluxDB instance.
    /// Fire-and-forget pattern - errors are logged but don't block test execution.
    /// </summary>
    public sealed class InfluxDBWriter : IInfluxDBWriter, IDisposable
    {
        private readonly InfluxDBOptions _options;
        private readonly HttpClient _httpClient;
        private readonly ILogger<InfluxDBWriter> _logger;
        private readonly bool _isEnabled;

        public InfluxDBWriter(
            InfluxDBOptions options,
            ILogger<InfluxDBWriter> logger,
            HttpClient httpClient)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // Only enable if customer configured InfluxDB properly
            _isEnabled = options.Enabled &&
                         !string.IsNullOrWhiteSpace(options.Url) &&
                         !string.IsNullOrWhiteSpace(options.Token) &&
                         !string.IsNullOrWhiteSpace(options.Organization) &&
                         !string.IsNullOrWhiteSpace(options.Bucket);

            if (_isEnabled)
            {
                ConfigureHttpClient();
                _logger.LogInformation(
                    "InfluxDBWriter initialized. URL: {Url}, Org: {Org}, Bucket: {Bucket}",
                    _options.Url,
                    _options.Organization,
                    _options.Bucket);
            }
            else
            {
                _logger.LogInformation("InfluxDBWriter is disabled or not configured.");
            }
        }

        public async Task UploadWindowedMetricsAsync(WindowedIterationSnapshot snapshot)
        {
            if (!_isEnabled)
            {
                return;
            }

            try
            {
                var lineProtocol = InfluxDBLineProtocolConverter.ConvertWindowedSnapshot(snapshot);

                if (string.IsNullOrWhiteSpace(lineProtocol))
                {
                    _logger.LogDebug("No windowed data to upload for iteration {IterationName}", snapshot.IterationName);
                    return;
                }

                await WriteToInfluxDBAsync(lineProtocol);

                _logger.LogDebug(
                    "Uploaded windowed metrics for {IterationName} (window {WindowSequence})",
                    snapshot.IterationName,
                    snapshot.WindowSequence);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to upload windowed metrics for {IterationName}. Test execution continues.",
                    snapshot.IterationName);
            }
        }

        public async Task UploadCumulativeMetricsAsync(CumulativeIterationSnapshot snapshot)
        {
            if (!_isEnabled)
            {
                return;
            }

            try
            {
                var lineProtocol = InfluxDBLineProtocolConverter.ConvertCumulativeSnapshot(snapshot);

                if (string.IsNullOrWhiteSpace(lineProtocol))
                {
                    _logger.LogDebug("No cumulative data to upload for iteration {IterationName}", snapshot.IterationName);
                    return;
                }

                await WriteToInfluxDBAsync(lineProtocol);

                _logger.LogDebug(
                    "Uploaded cumulative metrics for {IterationName} (final: {IsFinal})",
                    snapshot.IterationName,
                    snapshot.IsFinal);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to upload cumulative metrics for {IterationName}. Test execution continues.",
                    snapshot.IterationName);
            }
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_options.Url);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {_options.Token}");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        private async Task WriteToInfluxDBAsync(string lineProtocol)
        {
            var url = $"/api/v2/write?org={Uri.EscapeDataString(_options.Organization)}&bucket={Uri.EscapeDataString(_options.Bucket)}&precision=ns";
            var content = new StringContent(lineProtocol, Encoding.UTF8, "text/plain");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "InfluxDB write failed. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode,
                    errorBody);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
