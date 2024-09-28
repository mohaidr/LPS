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
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.SampleResponseServices;
using Microsoft.Extensions.Caching.Memory;

namespace LPS.Infrastructure.LPSClients
{
    public class HttpClientService : IClientService<HttpRequestProfile, HttpResponse>
    {
        private HttpClient httpClient;
        private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
        private ILogger _logger;
        private static int _clientNumber;
        public string Id { get; private set; }
        public string GuidId { get; private set; }
        private IClientConfiguration<HttpRequestProfile> _config;

        private readonly ICacheService<string> _memoryCacheService;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IMetricsService _metricsService;
        IHttpHeadersService _headersService;
        IMessageService _messageService;
        IResponseProcessingService _responseProcessingService = null;
        public HttpClientService(IClientConfiguration<HttpRequestProfile> config,
            ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMessageService messageService = null,
            IHttpHeadersService headersService = null,
            IMetricsService metricsService = null,
            ICacheService<string> memoryCacheService = null,
            IResponseProcessingService responseProcessingService = null)
        {
            _config = config;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _memoryCacheService = memoryCacheService;
            _headersService = headersService ?? new HttpHeadersService();
            _messageService = messageService ?? new MessageService(_headersService);
            _metricsService = metricsService ?? new MetricsService(_logger, _runtimeOperationIdProvider);
            _memoryCacheService = memoryCacheService ?? new MemoryCacheService<string>(new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = 1024
            }));
            _responseProcessingService = responseProcessingService ?? new ResponseProcessingService(_runtimeOperationIdProvider, _logger, _memoryCacheService);
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

            await _metricsService.AddMetricsAsync(lpsHttpRequestProfile.Id);
            int sequenceNumber = lpsHttpRequestProfile.LastSequenceId;
            HttpResponse lpsHttpResponse;
            Stopwatch stopWatch = new Stopwatch();
            try
            {
                var httpRequestMessage = _messageService.Build(lpsHttpRequestProfile);
                await _metricsService.TryIncreaseConnectionsCountAsync(lpsHttpRequestProfile, token);
                stopWatch.Start();
                var response = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, token);
                stopWatch.Stop();
                var results = (await ProcessResponseAsync(response, lpsHttpRequestProfile, sequenceNumber, token));
                HttpResponse.SetupCommand responseCommand = results.command;
                //This will only run if save response is set to true
                stopWatch.Start();
                await TryDownloadHtmlResourcesAsync(responseCommand, lpsHttpRequestProfile, httpClient, token);
                stopWatch.Stop();
                responseCommand.ResponseTime = stopWatch.Elapsed + results.streamTime; // set the response time after the complete payload is read.

                lpsHttpResponse = new HttpResponse(responseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.SetHttpRequestProfile(lpsHttpRequestProfile);

                await _metricsService.TryUpdateResponseMetricsAsync(lpsHttpRequestProfile, lpsHttpResponse, token);

                await _metricsService.TryDecreaseConnectionsCountAsync(lpsHttpRequestProfile, response.IsSuccessStatusCode, token);

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {Id} - Request # {sequenceNumber} {lpsHttpRequestProfile.HttpMethod} {lpsHttpRequestProfile.URL} Http/{lpsHttpRequestProfile.Httpversion}\n\tTotal Time: {responseCommand.ResponseTime.TotalMilliseconds} MS\n\tStatus Code: {(int)response.StatusCode} Reason: {response.StatusCode}\n\tResponse Body: {responseCommand.LocationToResponse}\n\tResponse Headers: {response.Headers}{response.Content.Headers}", LPSLoggingLevel.Verbose, token);

            }
            catch (Exception ex)
            {
                await _metricsService.TryDecreaseConnectionsCountAsync(lpsHttpRequestProfile, false, token);

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
                await _metricsService.TryUpdateResponseMetricsAsync(lpsHttpRequestProfile, lpsHttpResponse, token);


                if (ex.Message.Contains("socket") || ex.Message.Contains("buffer") || ex.InnerException != null && (ex.InnerException.Message.Contains("socket") || ex.InnerException.Message.Contains("buffer")))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"Client: {Id} - Request # {sequenceNumber} {lpsHttpRequestProfile.HttpMethod} {lpsHttpRequestProfile.URL} Http/{lpsHttpRequestProfile.Httpversion} \n\t  The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Critical, token);
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"...Client: {Id} - Request # {sequenceNumber} {lpsHttpRequestProfile.HttpMethod} {lpsHttpRequestProfile.URL} Http/{lpsHttpRequestProfile.Httpversion} \n\t The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, token);
                throw;
            }

            return lpsHttpResponse;
        }

        private async Task<(HttpResponse.SetupCommand command, TimeSpan streamTime)> ProcessResponseAsync(
            HttpResponseMessage response,
            HttpRequestProfile lpsHttpRequestProfile,
            int sequenceNumber,
            CancellationToken token)
        {
            Stopwatch streamStopwatch = Stopwatch.StartNew();
            string contentType = response?.Content?.Headers?.ContentType?.MediaType;
            MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);

            try
            {
                if (response.Content == null)
                {
                    throw new InvalidOperationException("Response content is null.");
                }

                string cacheKey = $"HtmlContent_{lpsHttpRequestProfile.Id}";
                string content = await _memoryCacheService.GetItemAsync(cacheKey);

                using Stream contentStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using var timeoutCts = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

                MemoryStream memoryStream = null;
                byte[] buffer = _bufferPool.Rent(64000);
                string locationToResponse = null;

                try
                {
                    // Initialize memoryStream if caching is needed
                    if (content == null && mimeType == MimeType.TextHtml && lpsHttpRequestProfile.DownloadHtmlEmbeddedResources)
                    {
                        memoryStream = new MemoryStream();
                    }

                    // Get the response processor
                    IResponseProcessor responseProcessor = await _responseProcessingService.CreateResponseProcessorAsync(
                        lpsHttpRequestProfile.URL, mimeType, lpsHttpRequestProfile.SaveResponse, token);

                    await using (responseProcessor.ConfigureAwait(false))
                    {
                        int bytesRead;
                        streamStopwatch.Start();

                        while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), linkedCts.Token)) > 0)
                        {
                            streamStopwatch.Stop();

                            // Write to memoryStream for caching
                            if (memoryStream != null)
                            {
                                  await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), linkedCts.Token);
                            }

                            // Process the chunk with the responseProcessor
                             await responseProcessor.ProcessResponseChunkAsync(buffer, 0, bytesRead, token);

                            streamStopwatch.Start();
                        }

                        streamStopwatch.Stop();

                        // Get the response file path if available
                        locationToResponse = responseProcessor.ResponseFilePath;
                    }

                    // Cache the content once fully read
                    if (memoryStream != null)
                    {
                        content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                        await _memoryCacheService.SetItemAsync(cacheKey, content);
                    }
                }
                finally
                {
                    _bufferPool.Return(buffer);
                    if (memoryStream != null)
                    {
                        await memoryStream.DisposeAsync();
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
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Error in ProcessResponseAsync: {ex.Message}", LPSLoggingLevel.Error, token);
                throw;
            }
        }

        private async Task<bool> TryDownloadHtmlResourcesAsync(HttpResponse.SetupCommand responseCommand, HttpRequestProfile lpsHttpRequestProfile, HttpClient client, CancellationToken token = default)
        {
            if (!lpsHttpRequestProfile.DownloadHtmlEmbeddedResources)
            {
                return false;
            }

            try
            {
                if (responseCommand.ContentType == MimeType.TextHtml && responseCommand.IsSuccessStatusCode && lpsHttpRequestProfile.Id == responseCommand.LPSHttpRequestProfileId)
                {
                    var htmlResourceDownloader = new HtmlResourceDownloaderService(_logger, _runtimeOperationIdProvider, new UrlSanitizationService(), client, _memoryCacheService);
                    await htmlResourceDownloader.DownloadResourcesAsync(lpsHttpRequestProfile.URL, lpsHttpRequestProfile.Id, token);
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



