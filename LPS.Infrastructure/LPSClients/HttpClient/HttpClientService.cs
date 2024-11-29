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
using LPS.Domain.LPSFlow.LPSHandlers;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.PlaceHolderService;
using LPS.Infrastructure.LPSClients.GlobalVariableManager;
using Microsoft.Extensions.Options;

namespace LPS.Infrastructure.LPSClients
{
    public class HttpClientService : IClientService<HttpRequest, HttpResponse>
    {
        readonly HttpClient httpClient;
        readonly ILogger _logger;
        private static int _clientNumber;
        public string SessionId { get; private set; }
        public string GuidId { get; private set; }
        readonly ICacheService<string> _memoryCacheService;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IMetricsService _metricsService;
        readonly IMessageService _messageService;
        readonly IResponseProcessingService _responseProcessingService;
        readonly ISessionManager _sessionManager;
        readonly IVariableManager _variableManager;
        readonly object _lock = new();
        public HttpClientService(IClientConfiguration<HttpRequest> config,
            ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ICacheService<string> memoryCacheService,
            ISessionManager sessionManager,
            IMessageService messageService,
            IMetricsService metricsService,
            IResponseProcessingService responseProcessingService,
            IVariableManager variableManager)
        {
            ArgumentNullException.ThrowIfNull(config, nameof(config));
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _memoryCacheService = memoryCacheService;
            _metricsService = metricsService;
            _memoryCacheService = memoryCacheService;
            _sessionManager = sessionManager;
            _messageService = messageService;
            _responseProcessingService = responseProcessingService;
            _variableManager = variableManager;
            SocketsHttpHandler socketsHandler = new()
            {
                PooledConnectionLifetime = ((ILPSHttpClientConfiguration<HttpRequest>)config).PooledConnectionLifetime,
                PooledConnectionIdleTimeout = ((ILPSHttpClientConfiguration<HttpRequest>)config).PooledConnectionIdleTimeout,
                MaxConnectionsPerServer = ((ILPSHttpClientConfiguration<HttpRequest>)config).MaxConnectionsPerServer,
                UseCookies = true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                EnableMultipleHttp2Connections = true,
            };
            httpClient = new HttpClient(socketsHandler)
            {
                Timeout = ((ILPSHttpClientConfiguration<HttpRequest>)config).Timeout
            };
            lock (_lock)
            {
                SessionId = _clientNumber++.ToString();
            }
            GuidId = Guid.NewGuid().ToString();
        }

        public async Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken token = default)
        {
            
            int sequenceNumber = request.LastSequenceId;
            HttpResponse lpsHttpResponse;
            Stopwatch stopWatch = new();
            try
            {
                var httpRequestMessage = await _messageService.BuildAsync(request, this.SessionId, token);
                await _metricsService.TryIncreaseConnectionsCountAsync(request.Id, token);
                stopWatch.Start();
                var responseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, token);
                stopWatch.Stop();
                string contentType = responseMessage?.Content?.Headers?.ContentType?.MediaType;
                MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);
                bool captureResponse = request.Capture != null && request.Capture.IsValid;
                bool cacheResponse = (mimeType == MimeType.TextHtml && request.DownloadHtmlEmbeddedResources) || captureResponse;
                var (command, streamTime) = (await _responseProcessingService.ProcessResponseAsync(responseMessage, request, cacheResponse, token));
                HttpResponse.SetupCommand responseCommand = command;
                //This will only run if save response is set to true
                if (captureResponse)
                {
                    MimeType @as = MimeTypeExtensions.FromKeyword(request.Capture.As);

                    var rawContent = await _memoryCacheService.GetItemAsync($"Content_{request.Id}");
                    if (rawContent != null)
                    {
                        var builder = new VariableHolder.Builder();
                        var variableHolder = builder.Build();
                        variableHolder = builder
                            .WithFormat(variableHolder.CheckIfKnownSupportedFormat(mimeType) ? mimeType:
                                        variableHolder.CheckIfKnownSupportedFormat(@as)? @as: MimeType.Unknown)
                            .WithPattern(request.Capture.Regex)
                            .WithRawResponse(rawContent).Build();

                        if (request.Capture.MakeGlobal)
                        {
                            variableHolder = builder.SetGlobal(true)
                                .Build();
                            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Setting {rawContent} to {request.Capture.Name} as a global variable", LPSLoggingLevel.Verbose, token);
                            await _variableManager.AddVariableAsync(request.Capture.Name, variableHolder, token);
                        }
                        else
                        {
                            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Setting {rawContent} to {request.Capture.Name} under Session {this.SessionId}", LPSLoggingLevel.Verbose, token);
                            await _sessionManager.AddResponseAsync(this.SessionId, request.Capture.Name, variableHolder, token);
                        }
                    }
                    else
                    {
                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "The client is unable to capture the response because the format is unknown or the content is empty.", LPSLoggingLevel.Warning, token);
                    }
                }
                stopWatch.Start();
                await TryDownloadHtmlResourcesAsync(responseCommand, request, httpClient, token);
                stopWatch.Stop();
                responseCommand.ResponseTime = stopWatch.Elapsed + streamTime; // set the response time after the complete payload is read.

                lpsHttpResponse = new HttpResponse(responseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.SetHttpRequest(request);

                await _metricsService.TryUpdateResponseMetricsAsync(request.Id, lpsHttpResponse, token);

                await _metricsService.TryDecreaseConnectionsCountAsync(request.Id, responseMessage.IsSuccessStatusCode, token);

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {SessionId} - Request # {sequenceNumber} {request.HttpMethod} {request.URL} Http/{request.HttpVersion}\n\tTotal Time: {responseCommand.ResponseTime.TotalMilliseconds} MS\n\tStatus Code: {(int)responseMessage.StatusCode} Reason: {responseMessage.StatusCode}\n\tResponse Body: {responseCommand.LocationToResponse}\n\tResponse Headers: {responseMessage.Headers}{responseMessage.Content.Headers}", LPSLoggingLevel.Verbose, token);
            }
            catch (Exception ex)
            {
                await _metricsService.TryDecreaseConnectionsCountAsync(request.Id, false, token);

                HttpResponse.SetupCommand lpsResponseCommand = new()
                {
                    StatusCode = 0,
                    StatusMessage = ex?.InnerException?.Message ?? ex?.Message,
                    LocationToResponse = string.Empty,
                    IsSuccessStatusCode = false,
                    HttpRequestId = request.Id,
                    ResponseTime = stopWatch.Elapsed,
                };

                lpsHttpResponse = new HttpResponse(lpsResponseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.SetHttpRequest(request);
                await _metricsService.TryUpdateResponseMetricsAsync(request.Id, lpsHttpResponse, token);


                if (ex.Message.Contains("socket") || ex.Message.Contains("buffer") || ex.InnerException != null && (ex.InnerException.Message.Contains("socket") || ex.InnerException.Message.Contains("buffer")))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"Client: {SessionId} - Request # {sequenceNumber} {request.HttpMethod} {request.URL} Http/{request.HttpVersion} \n\t  The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Critical, token);
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"...Client: {SessionId} - Request # {sequenceNumber} {request.HttpMethod} {request.URL} Http/{request.HttpVersion} \n\t The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, token);
                throw;
            }

            return lpsHttpResponse;
        }

        private async Task<bool> TryDownloadHtmlResourcesAsync(HttpResponse.SetupCommand responseCommand, HttpRequest httpRequest, HttpClient client, CancellationToken token = default)
        {
            if (!httpRequest.DownloadHtmlEmbeddedResources)
            {
                return false;
            }

            try
            {
                if (responseCommand.ContentType == MimeType.TextHtml && responseCommand.IsSuccessStatusCode && httpRequest.Id == responseCommand.HttpRequestId)
                {
                    var htmlResourceDownloader = new HtmlResourceDownloaderService(_logger, _runtimeOperationIdProvider, client, _memoryCacheService);
                    await htmlResourceDownloader.DownloadResourcesAsync(httpRequest.URL, httpRequest.Id, token);
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



