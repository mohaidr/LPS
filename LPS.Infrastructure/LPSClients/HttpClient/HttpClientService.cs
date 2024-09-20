using LPS.Domain;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.HeaderServices;
using LPS.Infrastructure.LPSClients.EmbeddedResourcesServices;
using LPS.Infrastructure.LPSClients.URLServices;
using LPS.Infrastructure.LPSClients.Metrics;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.LPSClients.MetricsServices;
using LPS.Infrastructure.LPSClients.MessageServices;
using System.Buffers;

namespace LPS.Infrastructure.LPSClients
{
    public class HttpClientService : IClientService<HttpRequestProfile, HttpResponse>
    {
        private HttpClient httpClient;
        private ILogger _logger;
        private static int _clientNumber;
        public string Id { get; private set; }
        public string GuidId { get; private set; }
        private IClientConfiguration<HttpRequestProfile> _config;

        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IMetricsService _metricsService;
        IHttpHeadersService _headersService;
        IMessageService _messageService;
        public HttpClientService(IClientConfiguration<HttpRequestProfile> config,
            ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMessageService messageService = null,
            IHttpHeadersService headersService = null,
            IMetricsService metricsService = null)
        {
            _config = config;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _headersService = headersService ?? new HttpHeadersService();
            _messageService = messageService ?? new MessageService(_headersService);
            _metricsService = metricsService ?? new MetricsService(_logger, _runtimeOperationIdProvider);
            SocketsHttpHandler socketsHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = ((ILPSHttpClientConfiguration<HttpRequestProfile>)config).PooledConnectionLifetime,
                PooledConnectionIdleTimeout = ((ILPSHttpClientConfiguration<HttpRequestProfile>)config).PooledConnectionIdleTimeout,
                MaxConnectionsPerServer = ((ILPSHttpClientConfiguration<HttpRequestProfile>)config).MaxConnectionsPerServer,
                UseCookies = true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                EnableMultipleHttp2Connections = true,
            };
            httpClient = new HttpClient(socketsHandler)
            {
                DefaultRequestVersion = HttpVersion.Version20
            };
            httpClient.Timeout = ((ILPSHttpClientConfiguration<HttpRequestProfile>)config).Timeout;
            Id = Interlocked.Increment(ref _clientNumber).ToString();
            GuidId = Guid.NewGuid().ToString();
        }
        public async Task<HttpResponse> SendAsync(HttpRequestProfile lpsHttpRequestProfile, CancellationToken token)
        {

            _metricsService.AddMetrics(lpsHttpRequestProfile.Id);
            int sequenceNumber = lpsHttpRequestProfile.LastSequenceId;
            HttpResponse lpsHttpResponse;
            Stopwatch stopWatch = new Stopwatch();
            try
            {
                var httpRequestMessage = _messageService.Build(lpsHttpRequestProfile);
                await _metricsService.TryIncreaseConnectionsCount(lpsHttpRequestProfile, token);
                stopWatch.Start();
                var response = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, token);
                stopWatch.Stop();
                var results = (await ProcessResponseAsync(response, lpsHttpRequestProfile, sequenceNumber, token));
                HttpResponse.SetupCommand responseCommand = results.command;
                //This will only run if save response is set to true
                stopWatch.Start();
                await TryDownloadHtmlResourcesAsync(responseCommand, lpsHttpRequestProfile, responseCommand.LocationToResponse, httpClient, token);
                stopWatch.Stop();
                responseCommand.ResponseTime = stopWatch.Elapsed + results.streamTime; // set the response time after the complete payload is read.

                lpsHttpResponse = new HttpResponse(responseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.SetHttpRequestProfile(lpsHttpRequestProfile);

                await _metricsService.TryUpdateResponseMetrics(lpsHttpRequestProfile, lpsHttpResponse, token);

                await _metricsService.TryDecreaseConnectionsCount(lpsHttpRequestProfile, response.IsSuccessStatusCode, token);

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {Id} - Request # {sequenceNumber} {lpsHttpRequestProfile.HttpMethod} {lpsHttpRequestProfile.URL} Http/{lpsHttpRequestProfile.Httpversion}\n\tTotal Time: {responseCommand.ResponseTime.TotalMilliseconds} MS\n\tStatus Code: {(int)response.StatusCode} Reason: {response.StatusCode}\n\tResponse Body: {responseCommand.LocationToResponse}\n\tResponse Headers: {response.Headers}{response.Content.Headers}", LPSLoggingLevel.Verbose, token);

            }
            catch (Exception ex)
            {
                await _metricsService.TryDecreaseConnectionsCount(lpsHttpRequestProfile, false, token);

                HttpResponse.SetupCommand lpsResponseCommand = new HttpResponse.SetupCommand
                {
                    StatusCode = 0,
                    StatusMessage = ex?.InnerException?.Message,
                    LocationToResponse = string.Empty,
                    IsSuccessStatusCode = false,
                    LPSHttpRequestProfileId = lpsHttpRequestProfile.Id,
                    ResponseTime = stopWatch.Elapsed,
                };

                lpsHttpResponse = new HttpResponse(lpsResponseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.SetHttpRequestProfile(lpsHttpRequestProfile);
                await _metricsService.TryUpdateResponseMetrics(lpsHttpRequestProfile, lpsHttpResponse, token);


                if (ex.Message.Contains("socket") || ex.Message.Contains("buffer") || ex.InnerException != null && (ex.InnerException.Message.Contains("socket") || ex.InnerException.Message.Contains("buffer")))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"Client: {Id} - Request # {sequenceNumber} {lpsHttpRequestProfile.HttpMethod} {lpsHttpRequestProfile.URL} Http/{lpsHttpRequestProfile.Httpversion} \n\t  The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Critical, token);
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"...Client: {Id} - Request # {sequenceNumber} {lpsHttpRequestProfile.HttpMethod} {lpsHttpRequestProfile.URL} Http/{lpsHttpRequestProfile.Httpversion} \n\t The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, token);
                throw;
            }

            return lpsHttpResponse;
        }

        private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
        private async Task<(HttpResponse.SetupCommand command, TimeSpan streamTime)> ProcessResponseAsync(
            HttpResponseMessage response,
            HttpRequestProfile lpsHttpRequestProfile,
            int sequenceNumber,
            CancellationToken token)
        {
            Stopwatch streamStopwatch = Stopwatch.StartNew();
            string locationToResponse = string.Empty;
            string contentType = response?.Content?.Headers?.ContentType?.MediaType;
            MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);

            try
            {
                // Ensure response.Content is not null
                if (response.Content == null)
                {
                    throw new InvalidOperationException("Response content is null.");
                }

                using Stream contentStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using var timeoutCts = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                {
                    timeoutCts.CancelAfter(((ILPSHttpClientConfiguration<HttpRequestProfile>)_config).Timeout);
                    byte[] buffer = _bufferPool.Rent(64000);
                    try
                    {
                        // Determine the target stream based on whether to save the response
                        Stream targetStream = Stream.Null; // Default to discarding data

                        if (lpsHttpRequestProfile.SaveResponse)
                        {
                            // Build the file path for saving the response
                            string fileExtension = mimeType.ToFileExtension();
                            string sanitizedUrl = new UrlSanitizationService().Sanitize(lpsHttpRequestProfile.URL);
                            string directoryName = $"{_runtimeOperationIdProvider.OperationId}.{sanitizedUrl}.Resources";

                            Directory.CreateDirectory(directoryName);
                            locationToResponse = $"{directoryName}/{Id}.{sequenceNumber}.{lpsHttpRequestProfile.Id}.{sanitizedUrl}.http {response.Version} {fileExtension}";

                            // Initialize FileStream if SaveResponse is true
                            targetStream = new FileStream(
                                locationToResponse,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.None,
                                bufferSize: 4096,
                                useAsync: true);
                        }

                        await using (targetStream.ConfigureAwait(false))
                        {
                            int bytesRead;
                            streamStopwatch.Start();

                            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), linkedCts.Token).ConfigureAwait(false)) > 0)
                            {
                                streamStopwatch.Stop();
                                // Save the response to the target stream (e.g., file). If stream is set to Stream.Null, the below line will be discarded
                                await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead), linkedCts.Token).ConfigureAwait(false);
                                streamStopwatch.Start();
                            }

                            streamStopwatch.Stop();

                            await targetStream.FlushAsync(linkedCts.Token).ConfigureAwait(false);
                        }

                    }

                    finally
                    {
                        _bufferPool.Return(buffer);
                    }
                }

                return (new HttpResponse.SetupCommand
                {
                    StatusCode = response.StatusCode,
                    StatusMessage = response.ReasonPhrase,
                    LocationToResponse = locationToResponse,
                    IsSuccessStatusCode = response.IsSuccessStatusCode,
                    ResponseContentHeaders = response.Content?.Headers?.ToDictionary(header => header.Key, header => string.Join(", ", header.Value)),
                    ResponseHeaders = response.Headers?.ToDictionary(header => header.Key, header => string.Join(", ", header.Value)),
                    ContentType = mimeType,
                    LPSHttpRequestProfileId = lpsHttpRequestProfile.Id,
                }, streamStopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        private async Task<bool> TryDownloadHtmlResourcesAsync(HttpResponse.SetupCommand responseCommand, HttpRequestProfile lpsHttpRequestProfile, string htmlFilePath, HttpClient client, CancellationToken token = default)
        {
            if (!lpsHttpRequestProfile.DownloadHtmlEmbeddedResources)
            {
                return false;
            }
            if (!lpsHttpRequestProfile.SaveResponse)
            {
                _ = _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Save Response must be set to true to download the embedded html resources", LPSLoggingLevel.Warning, token);
                return false;
            }
            try
            {
                if (responseCommand.ContentType == MimeType.TextHtml && responseCommand.IsSuccessStatusCode && lpsHttpRequestProfile.Id == responseCommand.LPSHttpRequestProfileId)
                {
                    var htmlResourceDownloader = new HtmlResourceDownloaderService(_logger, _runtimeOperationIdProvider, new UrlSanitizationService(), client);
                    await htmlResourceDownloader.DownloadResourcesAsync(lpsHttpRequestProfile.URL, htmlFilePath, token);
                }
                return true;
            }
            catch (Exception ex)
            {

                _ = _logger.LogAsync(_runtimeOperationIdProvider.OperationId, ex.Message, LPSLoggingLevel.Error, token);
                return false;
            }
        }

    }
}



