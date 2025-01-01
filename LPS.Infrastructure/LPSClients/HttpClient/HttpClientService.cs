using LPS.Domain;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.EmbeddedResourcesServices;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.LPSClients.MetricsServices;
using LPS.Infrastructure.LPSClients.MessageServices;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.ResponseService;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.LPSClients.GlobalVariableManager;
using LPS.Infrastructure.LPSClients.CachService;

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
        readonly IPlaceholderResolverService _placeholderResolverService;
        readonly object _lock = new();
        public HttpClientService(IClientConfiguration<HttpRequest> config,
            ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ICacheService<string> memoryCacheService,
            ISessionManager sessionManager,
            IMessageService messageService,
            IMetricsService metricsService,
            IResponseProcessingService responseProcessingService,
            IVariableManager variableManager,
            IPlaceholderResolverService placeholderResolver)
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
            _placeholderResolverService = placeholderResolver;
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

        public async Task<HttpResponse> SendAsync(HttpRequest httpRequestEntity, CancellationToken token = default)
        {
            int sequenceNumber = httpRequestEntity.LastSequenceId;
            HttpResponse lpsHttpResponse;
            Stopwatch uploadWatch = new();
            TimeSpan initialStreamTime = TimeSpan.FromSeconds(0);
            TimeSpan initialHtmlDownloadTime = TimeSpan.FromSeconds(0);
            try
            {
                using var timeoutCts = new CancellationTokenSource();
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                    {
                        timeoutCts.CancelAfter(httpClient.Timeout);

                        #region Build The Message
                        var (httpRequestMessage, dataSentSize) = await _messageService.BuildAsync(httpRequestEntity, this.SessionId, linkedCts.Token);
                        httpRequestMessage.Content = httpRequestMessage.Content != null ? await WrapWithProgressContentAsync(httpRequestEntity, httpRequestMessage.Content, uploadWatch, linkedCts.Token) : httpRequestMessage.Content;
                        #endregion
                        //Update Throughput Metric
                        await _metricsService.TryIncreaseConnectionsCountAsync(httpRequestEntity.Id, token);
                        //Start the http Request
                        var responseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
                        //Update Data Transmission Metrics
                        //await _metricsService.TryUpdateDataSentAsync(request.Id, dataSentSize, uploadWatch.ElapsedMilliseconds, linkedCts.Token);

                        #region Take Content Caching DecesionDecesion
                        string contentType = responseMessage?.Content?.Headers?.ContentType?.MediaType;
                        MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);
                        bool captureResponse = httpRequestEntity.Capture != null && httpRequestEntity.Capture.IsValid;
                        bool cacheResponse = (mimeType == MimeType.TextHtml && httpRequestEntity.DownloadHtmlEmbeddedResources) || captureResponse;
                        #endregion

                        //Read the Response and total time spent to read the data represented as timespan
                        // TODO: Test moving the download and upload bytes metric to the ProgressContent and the  ResponseProcessingService Service to let the dashboard reflect them instantly for large payloads
                        var (command, dataReceivedSize, streamTime) = (await _responseProcessingService.ProcessResponseAsync(responseMessage, httpRequestEntity, cacheResponse, linkedCts.Token));
                        initialStreamTime = streamTime;
                        HttpResponse.SetupCommand responseCommand = command;

                        #region Capture Response
                        if (captureResponse)
                        {
                            MimeType @as = MimeTypeExtensions.FromKeyword(httpRequestEntity.Capture.As);

                            var rawContent = await _memoryCacheService.GetItemAsync($"{CachePrefixes.Content}{httpRequestEntity.Id}");
                            if (rawContent != null)
                            {
                                var builder = new VariableHolder.Builder(_placeholderResolverService);
                                var variableHolder = await builder.BuildAsync(linkedCts.Token);
                                variableHolder = await builder
                                    .WithFormat(IVariableHolder.IsKnownSupportedFormat(mimeType) ? mimeType :
                                                IVariableHolder.IsKnownSupportedFormat(@as) ? @as : MimeType.Unknown)
                                    .WithPattern(httpRequestEntity.Capture.Regex)
                                    .WithRawValue(rawContent).BuildAsync(token);

                                if (httpRequestEntity.Capture.MakeGlobal == true)
                                {
                                    variableHolder = await builder.SetGlobal(true)
                                        .BuildAsync(token);
                                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Setting {(MimeTypeExtensions.IsTextContent(mimeType) ? rawContent : "BinaryContent ")} to {httpRequestEntity.Capture.To} as a global variable", LPSLoggingLevel.Verbose, linkedCts.Token);
                                    await _variableManager.AddVariableAsync(httpRequestEntity.Capture.To, variableHolder, token);
                                }
                                else
                                {
                                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Setting {(MimeTypeExtensions.IsTextContent(mimeType) ? rawContent : "BinaryContent ")} to {httpRequestEntity.Capture.To} under Session {this.SessionId}", LPSLoggingLevel.Verbose, linkedCts.Token);
                                    await _sessionManager.AddResponseAsync(this.SessionId, httpRequestEntity.Capture.To, variableHolder, linkedCts.Token);
                                }
                            }
                            else
                            {
                                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "The client is unable to capture the response because the format is either unknown or the content is empty.", LPSLoggingLevel.Warning, linkedCts.Token);
                            }

                            if (httpRequestEntity.Capture.Headers != null && httpRequestEntity.Capture.Headers.Any())
                            {
                                foreach (var headerName in httpRequestEntity.Capture.Headers)
                                {
                                    // Check if the response contains the header
                                    if (responseMessage.Headers.TryGetValues(headerName, out var headerValues) ||
                                        responseMessage.Content.Headers.TryGetValues(headerName, out headerValues))
                                    {
                                        // Combine multiple header values into a single string (if needed)
                                        string headerValue = string.Join(", ", headerValues);

                                        // Sanitize the header name to create a valid variable name
                                        string variableName = headerName.Replace("-", string.Empty);

                                        // Create a VariableHolder for the header
                                        var builder = new VariableHolder.Builder(_placeholderResolverService);
                                        var variableHolder = await builder
                                            .WithFormat(MimeType.TextPlain) // Assuming plain text for headers
                                            .WithRawValue(headerValue)
                                            .BuildAsync(token);

                                        // Store the variable based on the MakeGlobal option
                                        if (httpRequestEntity.Capture.MakeGlobal == true)
                                        {
                                            variableHolder = await builder.SetGlobal(true).BuildAsync(linkedCts.Token);
                                            await _logger.LogAsync(
                                                _runtimeOperationIdProvider.OperationId,
                                                $"Setting response header '{headerName}' with value '{headerValue}' as global variable '{variableName}'",
                                                LPSLoggingLevel.Verbose,
                                                linkedCts.Token
                                            );
                                            await _variableManager.AddVariableAsync(variableName, variableHolder, linkedCts.Token);
                                        }
                                        else
                                        {
                                            await _logger.LogAsync(
                                                _runtimeOperationIdProvider.OperationId,
                                                $"Setting response header '{headerName}' with value '{headerValue}' in session '{this.SessionId}' as variable '{variableName}'",
                                                LPSLoggingLevel.Verbose,
                                                linkedCts.Token
                                            );
                                            await _sessionManager.AddResponseAsync(this.SessionId, variableName, variableHolder, linkedCts.Token);
                                        }
                                    }
                                    else
                                    {
                                        // Log if the header was not found in the response
                                        await _logger.LogAsync(
                                            _runtimeOperationIdProvider.OperationId,
                                            $"Response does not contain the header '{headerName}' specified in the Capture.Headers list.",
                                            LPSLoggingLevel.Warning,
                                            linkedCts.Token
                                        );
                                    }
                                }
                            }
                        }
                        #endregion

                        // Download Html Embdedded Resources, conditonally
                        var (hasDownloaded, downloadTime) = await TryDownloadHtmlResourcesAsync(responseCommand, httpRequestEntity, httpClient, linkedCts.Token);
                        initialHtmlDownloadTime = downloadTime;
                        responseCommand.TotalTime = uploadWatch.Elapsed + downloadTime + streamTime; // set the response time after the complete payload is read.
                        // Data Transmission Metrics 
                        await _metricsService.TryUpdateDataReceivedAsync(httpRequestEntity.Id, dataReceivedSize, downloadTime.TotalMilliseconds, linkedCts.Token);

                        lpsHttpResponse = new HttpResponse(responseCommand, _logger, _runtimeOperationIdProvider);
                        lpsHttpResponse.SetHttpRequest(httpRequestEntity);

                        //Update Response Break Down Metrics
                        await _metricsService.TryUpdateResponseMetricsAsync(httpRequestEntity.Id, lpsHttpResponse, linkedCts.Token);

                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {SessionId} - Request # {sequenceNumber} {httpRequestMessage?.Method} {httpRequestMessage?.RequestUri} Http/{httpRequestMessage?.Version}\n\tTotal Time: {responseCommand?.TotalTime.TotalMilliseconds} MS\n\tStatus Code: {(int)responseMessage?.StatusCode} Reason: {responseMessage?.ReasonPhrase}\n\tResponse Body: {responseCommand?.LocationToResponse}\n\tResponse Headers: {responseMessage?.Headers}{responseMessage?.Content?.Headers}", LPSLoggingLevel.Verbose, token);
                        //Update Throughput Metrics
                        await _metricsService.TryDecreaseConnectionsCountAsync(httpRequestEntity.Id, responseMessage.IsSuccessStatusCode, linkedCts.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                //Decrease Connections On Failure
                await _metricsService.TryDecreaseConnectionsCountAsync(httpRequestEntity.Id, false, token);

                HttpResponse.SetupCommand lpsResponseCommand = new()
                {
                    StatusCode = 0,
                    StatusMessage = ex?.InnerException?.Message ?? ex?.Message,
                    LocationToResponse = string.Empty,
                    IsSuccessStatusCode = false,
                    HttpRequestId = httpRequestEntity.Id,
                    TotalTime = uploadWatch.Elapsed + initialHtmlDownloadTime + initialStreamTime,
                };

                lpsHttpResponse = new HttpResponse(lpsResponseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.SetHttpRequest(httpRequestEntity);
                await _metricsService.TryUpdateResponseMetricsAsync(httpRequestEntity.Id, lpsHttpResponse, token);


                if (ex.Message.Contains("socket") || ex.Message.Contains("buffer") || ex.InnerException != null && (ex.InnerException.Message.Contains("socket") || ex.InnerException.Message.Contains("buffer")))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"Client: {SessionId} - Request # {sequenceNumber} {httpRequestEntity.HttpMethod} {httpRequestEntity.Url.Url} Http/{httpRequestEntity.HttpVersion} \n\t  The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Critical, token);
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"...Client: {SessionId} - Request # {sequenceNumber} {httpRequestEntity.HttpMethod} {httpRequestEntity.Url.Url} Http/{httpRequestEntity.HttpVersion} \n\t The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, token);
                throw;
            }
            return lpsHttpResponse;
        }
        private async Task<HttpContent> WrapWithProgressContentAsync(HttpRequest request, HttpContent content, Stopwatch stopwatch, CancellationToken token)
        {
            var progress = new Progress<long>(async (bytesRead) =>
            {
                await _metricsService.TryUpdateDataSentAsync(request.Id, bytesRead, stopwatch.ElapsedMilliseconds, token);
                stopwatch.Restart();
                // TODO: We Need to log the number of read bytes to a different log file (stream.log)
            });

            // Perform any asynchronous initialization required
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            // Ensure the original content stream is prepared asynchronously (if needed)
            if (content is StreamContent streamContent)
            {
                await streamContent.LoadIntoBufferAsync(); // Example: Ensure the stream is fully loaded (optional)
            }
            return new ProgressContent(content, progress, stopwatch, token);
        }

        private async Task<(bool, TimeSpan)> TryDownloadHtmlResourcesAsync(HttpResponse.SetupCommand responseCommand, HttpRequest httpRequest, HttpClient client, CancellationToken token = default)
        {
            Stopwatch downloadWatch = new();

            if (!httpRequest.DownloadHtmlEmbeddedResources)
            {
                return (false, TimeSpan.FromSeconds(0));
            }

            try
            {
                if (responseCommand.ContentType == MimeType.TextHtml && responseCommand.IsSuccessStatusCode && httpRequest.Id == responseCommand.HttpRequestId)
                {
                    downloadWatch.Start();
                    var htmlResourceDownloader = new HtmlResourceDownloaderService(_logger, _runtimeOperationIdProvider, client, _memoryCacheService);
                    await htmlResourceDownloader.DownloadResourcesAsync(httpRequest.Url.Url, httpRequest.Id, token);
                    downloadWatch.Stop();
                }
                return (true, downloadWatch.Elapsed);
            }
            catch (Exception ex)
            {
                _ = _logger.LogAsync(_runtimeOperationIdProvider.OperationId, ex.Message, LPSLoggingLevel.Error, token);
                return (false, downloadWatch.Elapsed);
            }
        }
    }
}



