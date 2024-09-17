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
            IMessageService messageService= null,
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
        public async Task<HttpResponse> SendAsync(HttpRequestProfile lpsHttpRequestProfile, CancellationToken token = default)
        {

            _metricsService.AddMetrics(lpsHttpRequestProfile.Id);
            int sequenceNumber = lpsHttpRequestProfile.LastSequenceId;
            HttpResponse lpsHttpResponse;
            Stopwatch stopWatch = new Stopwatch();
            try
            {
                var httpRequestMessage = _messageService.Build(lpsHttpRequestProfile);
                await _metricsService.TryIncreaseConnectionsCount(lpsHttpRequestProfile, token);
                // restart in case StartNew adds unnecessary milliseconds becuase of JITing. See this https://stackoverflow.com/questions/14019510/calculate-the-execution-time-of-a-method
                stopWatch.Start();
                var response = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, token);
                stopWatch.Stop();
                var results = (await ProcessResponseAsync(response, lpsHttpRequestProfile, sequenceNumber, token));
                HttpResponse.SetupCommand responseCommand = results.command;
                //This will only run if save response is set to true
                await TryDownloadHtmlResourcesAsync(responseCommand, lpsHttpRequestProfile, responseCommand.LocationToResponse, httpClient);
                responseCommand.ResponseTime = stopWatch.Elapsed+ results.streamTime; // set the response time after the complete payload is read.
                stopWatch.Stop();

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

        private async Task<(HttpResponse.SetupCommand command, TimeSpan streamTime)> ProcessResponseAsync(HttpResponseMessage response, HttpRequestProfile lpsHttpRequestProfile, int sequenceNumber, CancellationToken token)
        {
            try
            {
                Stopwatch streamStopwatch = Stopwatch.StartNew();
                string locationToResponse = string.Empty;
                string contentType = response?.Content?.Headers?.ContentType?.MediaType;
                MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);

                using (Stream contentStream = await response.Content.ReadAsStreamAsync(token))
                {
                    var timeoutCts = new CancellationTokenSource();
                    timeoutCts.CancelAfter(((ILPSHttpClientConfiguration<HttpRequestProfile>)_config).Timeout);
                    var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                    byte[] buffer = new byte[64000]; // Adjust the buffer size as needed and modify this logic to have queue of buffers and reuse them
                    int bytesRead;
                    FileStream fileStream = null;
                    if (lpsHttpRequestProfile.SaveResponse)
                    {
                        string fileExtension = mimeType.ToFileExtension();
                        string sanitizedUrl = new UrlSanitizationService().Sanitize(lpsHttpRequestProfile.URL);
                        string directoryName = $"{_runtimeOperationIdProvider.OperationId}.{sanitizedUrl}.Resources";

                        Directory.CreateDirectory(directoryName);
                        locationToResponse = $"{directoryName}/{Id}.{sequenceNumber}.{lpsHttpRequestProfile.Id}.{sanitizedUrl}.http {response.Version} {fileExtension}";

                        fileStream = File.Create(locationToResponse);
                    }
                    streamStopwatch.Start();
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, linkedCts.Token)) > 0)
                    {
                        streamStopwatch.Stop();
                        await (fileStream?.WriteAsync(buffer, 0, bytesRead, linkedCts.Token) ?? Task.CompletedTask);
                        streamStopwatch.Start();
                    }
                    streamStopwatch.Stop();
                    await (fileStream?.FlushAsync(linkedCts.Token) ?? Task.CompletedTask);

                }
                return ( new HttpResponse.SetupCommand
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


        private async Task<bool> TryDownloadHtmlResourcesAsync(HttpResponse.SetupCommand responseCommand, HttpRequestProfile lpsHttpRequestProfile, string htmlFilePath, HttpClient client)
        {
            if (!lpsHttpRequestProfile.DownloadHtmlEmbeddedResources)
            { 
                return false;
            }
            if (!lpsHttpRequestProfile.SaveResponse)
            {
                _=_logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Save Response must be set to true to download the embedded html resources", LPSLoggingLevel.Warning);
                return false;
            }
            try
            {
                if (responseCommand.ContentType == MimeType.TextHtml && responseCommand.IsSuccessStatusCode && lpsHttpRequestProfile.Id == responseCommand.LPSHttpRequestProfileId)
                {
                    var htmlResourceDownloader = new HtmlResourceDownloaderService(_logger, _runtimeOperationIdProvider, new UrlSanitizationService(), client);
                    await htmlResourceDownloader.DownloadResourcesAsync(lpsHttpRequestProfile.URL, htmlFilePath, CancellationToken.None);
                }
                return true;
            }
            catch (Exception ex)
            {

                _ = _logger.LogAsync(_runtimeOperationIdProvider.OperationId, ex.Message, LPSLoggingLevel.Error);
                return false;
            }
        }

    }
}



