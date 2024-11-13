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
using LPS.Infrastructure.LPSClients.ResponseService;
using LPS.Infrastructure.Common.Interfaces;

namespace LPS.Infrastructure.LPSClients
{
    public class HttpClientService : IClientService<HttpSession, HttpResponse>
    {
        readonly HttpClient httpClient;
        readonly ILogger _logger;
        private static int _clientNumber;
        public string Id { get; private set; }
        public string GuidId { get; private set; }
        readonly ICacheService<string> _memoryCacheService;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IMetricsService _metricsService;
        readonly IHttpHeadersService _headersService;
        readonly IMessageService _messageService;
        readonly IResponseProcessingService _responseProcessingService;
        readonly IMetricsQueryService _metricsQueryService;
        readonly IUrlSanitizationService _urlSanitizationService;
        readonly object _lock = new();
        public HttpClientService(IClientConfiguration<HttpSession> config,
            ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsQueryService metricsQueryService,
            ICacheService<string> memoryCacheService,
            IUrlSanitizationService urlSanitizationService = null,
            IMessageService messageService = null,
            IHttpHeadersService headersService = null,
            IMetricsService metricsService = null,
            IResponseProcessingService responseProcessingService = null)
        {
            ArgumentNullException.ThrowIfNull(config, nameof(config));
            _metricsQueryService = metricsQueryService;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _memoryCacheService = memoryCacheService;
            _headersService = headersService ?? new HttpHeadersService();
            _metricsService = metricsService ?? new MetricsService(_logger, _runtimeOperationIdProvider, _metricsQueryService);
            _memoryCacheService = memoryCacheService;

            _urlSanitizationService = urlSanitizationService ?? new UrlSanitizationService();
            _messageService = messageService ?? new MessageService(_headersService, _metricsService, _logger, _runtimeOperationIdProvider);
            _responseProcessingService = responseProcessingService ?? new ResponseProcessingService(_memoryCacheService, _logger, _runtimeOperationIdProvider, _urlSanitizationService, _metricsService);
            SocketsHttpHandler socketsHandler = new()
            {
                PooledConnectionLifetime = ((ILPSHttpClientConfiguration<HttpSession>)config).PooledConnectionLifetime,
                PooledConnectionIdleTimeout = ((ILPSHttpClientConfiguration<HttpSession>)config).PooledConnectionIdleTimeout,
                MaxConnectionsPerServer = ((ILPSHttpClientConfiguration<HttpSession>)config).MaxConnectionsPerServer,
                UseCookies = true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                EnableMultipleHttp2Connections = true,
            };
            httpClient = new HttpClient(socketsHandler)
            {
                Timeout = ((ILPSHttpClientConfiguration<HttpSession>)config).Timeout
            };
            lock (_lock)
            {
                Id = _clientNumber++.ToString();
            }
            GuidId = Guid.NewGuid().ToString();
        }
        public async Task<HttpResponse> SendAsync(HttpSession session, CancellationToken token = default)
        {
            int sequenceNumber = session.LastSequenceId;
            HttpResponse lpsHttpResponse;
            Stopwatch stopWatch = new();
            try
            {
                var httpRequestMessage = await _messageService.BuildAsync(session, token);
                await _metricsService.TryIncreaseConnectionsCountAsync(session.Id, token);
                stopWatch.Start();
                var response = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, token);
                stopWatch.Stop();
                var (command, streamTime) = (await _responseProcessingService.ProcessResponseAsync(response, session, token));
                HttpResponse.SetupCommand responseCommand = command;
                //This will only run if save response is set to true
                stopWatch.Start();
                await TryDownloadHtmlResourcesAsync(responseCommand, session, httpClient, token);
                stopWatch.Stop();
                responseCommand.ResponseTime = stopWatch.Elapsed + streamTime; // set the response time after the complete payload is read.

                lpsHttpResponse = new HttpResponse(responseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.SetHttpSession(session);

                await _metricsService.TryUpdateResponseMetricsAsync(session.Id, lpsHttpResponse, token);

                await _metricsService.TryDecreaseConnectionsCountAsync(session.Id, response.IsSuccessStatusCode, token);

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {Id} - Request # {sequenceNumber} {session.HttpMethod} {session.URL} Http/{session.HttpVersion}\n\tTotal Time: {responseCommand.ResponseTime.TotalMilliseconds} MS\n\tStatus Code: {(int)response.StatusCode} Reason: {response.StatusCode}\n\tResponse Body: {responseCommand.LocationToResponse}\n\tResponse Headers: {response.Headers}{response.Content.Headers}", LPSLoggingLevel.Verbose, token);

            }
            catch (Exception ex)
            {
                await _metricsService.TryDecreaseConnectionsCountAsync(session.Id, false, token);

                HttpResponse.SetupCommand lpsResponseCommand = new HttpResponse.SetupCommand
                {
                    StatusCode = 0,
                    StatusMessage = ex?.InnerException?.Message ?? ex?.Message,
                    LocationToResponse = string.Empty,
                    IsSuccessStatusCode = false,
                    HttpSessionId = session.Id,
                    ResponseTime = stopWatch.Elapsed,
                };

                lpsHttpResponse = new HttpResponse(lpsResponseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.SetHttpSession(session);
                await _metricsService.TryUpdateResponseMetricsAsync(session.Id, lpsHttpResponse, token);


                if (ex.Message.Contains("socket") || ex.Message.Contains("buffer") || ex.InnerException != null && (ex.InnerException.Message.Contains("socket") || ex.InnerException.Message.Contains("buffer")))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"Client: {Id} - Request # {sequenceNumber} {session.HttpMethod} {session.URL} Http/{session.HttpVersion} \n\t  The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Critical, token);
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"...Client: {Id} - Request # {sequenceNumber} {session.HttpMethod} {session.URL} Http/{session.HttpVersion} \n\t The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, token);
                throw;
            }

            return lpsHttpResponse;
        }

        private async Task<bool> TryDownloadHtmlResourcesAsync(HttpResponse.SetupCommand responseCommand, HttpSession httpSession, HttpClient client, CancellationToken token = default)
        {
            if (!httpSession.DownloadHtmlEmbeddedResources)
            {
                return false;
            }

            try
            {
                if (responseCommand.ContentType == MimeType.TextHtml && responseCommand.IsSuccessStatusCode && httpSession.Id == responseCommand.HttpSessionId)
                {
                    var htmlResourceDownloader = new HtmlResourceDownloaderService(_logger, _runtimeOperationIdProvider, client, _memoryCacheService);
                    await htmlResourceDownloader.DownloadResourcesAsync(httpSession.URL, httpSession.Id, token);
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



