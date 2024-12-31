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

        public async Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken token = default)
        {
            int sequenceNumber = request.LastSequenceId;
            HttpResponse lpsHttpResponse;
            Stopwatch uploadWatch = new();
            Stopwatch downloadWatch = new();
            TimeSpan initialStreamTime = TimeSpan.FromSeconds(0);
            try
            {
                using var timeoutCts = new CancellationTokenSource();
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                    {
                        timeoutCts.CancelAfter(httpClient.Timeout);

                        #region Build The Message
                        var (httpRequestMessage, dataSentSize) = await _messageService.BuildAsync(request, this.SessionId, linkedCts.Token);
                        httpRequestMessage.Content = httpRequestMessage.Content != null? await WrapWithProgressContentAsync(httpRequestMessage.Content, uploadWatch, linkedCts.Token): httpRequestMessage.Content;
                        #endregion
                        //Update Throughput Metric
                        await _metricsService.TryIncreaseConnectionsCountAsync(request.Id, token);
                        //Start the http Request
                        var responseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
                        //Update Data Transmission Metrics
                        await _metricsService.TryUpdateDataSentAsync(request.Id, dataSentSize, uploadWatch.ElapsedMilliseconds, linkedCts.Token);

                        #region Take Content Caching DecesionDecesion
                        string contentType = responseMessage?.Content?.Headers?.ContentType?.MediaType;
                        MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);
                        bool captureResponse = request.Capture != null && request.Capture.IsValid;
                        bool cacheResponse = (mimeType == MimeType.TextHtml && request.DownloadHtmlEmbeddedResources) || captureResponse;
                        #endregion

                        //Read the Response and total time spent to read the data represented as timespan
                        // TODO: Test moving the download and upload bytes metric to the ProgressContent and the  ResponseProcessingService Service to let the dashboard reflect them instantly for large payloads
                        var (command, dataReceivedSize, streamTime) = (await _responseProcessingService.ProcessResponseAsync(responseMessage, request, cacheResponse, linkedCts.Token));
                        initialStreamTime = streamTime;
                        HttpResponse.SetupCommand responseCommand = command;

                        #region Capture Response
                        if (captureResponse)
                        {
                            MimeType @as = MimeTypeExtensions.FromKeyword(request.Capture.As);

                            var rawContent = await _memoryCacheService.GetItemAsync($"{CachePrefixes.Content}{request.Id}");
                            if (rawContent != null)
                            {
                                var builder = new VariableHolder.Builder(_placeholderResolverService);
                                var variableHolder = await builder.BuildAsync(linkedCts.Token);
                                variableHolder = await builder
                                    .WithFormat(IVariableHolder.IsKnownSupportedFormat(mimeType) ? mimeType :
                                                IVariableHolder.IsKnownSupportedFormat(@as) ? @as : MimeType.Unknown)
                                    .WithPattern(request.Capture.Regex)
                                    .WithRawValue(rawContent).BuildAsync(token);

                                if (request.Capture.MakeGlobal == true)
                                {
                                    variableHolder = await builder.SetGlobal(true)
                                        .BuildAsync(token);
                                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Setting {(MimeTypeExtensions.IsTextContent(mimeType) ? rawContent : "BinaryContent ")} to {request.Capture.To} as a global variable", LPSLoggingLevel.Verbose, linkedCts.Token);
                                    await _variableManager.AddVariableAsync(request.Capture.To, variableHolder, token);
                                }
                                else
                                {
                                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Setting {(MimeTypeExtensions.IsTextContent(mimeType) ? rawContent : "BinaryContent ")} to {request.Capture.To} under Session {this.SessionId}", LPSLoggingLevel.Verbose, linkedCts.Token);
                                    await _sessionManager.AddResponseAsync(this.SessionId, request.Capture.To, variableHolder, linkedCts.Token);
                                }
                            }
                            else
                            {
                                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "The client is unable to capture the response because the format is either unknown or the content is empty.", LPSLoggingLevel.Warning, linkedCts.Token);
                            }

                            if (request.Capture.Headers != null && request.Capture.Headers.Any())
                            {
                                foreach (var headerName in request.Capture.Headers)
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
                                        if (request.Capture.MakeGlobal == true)
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
                        downloadWatch.Start();
                        await TryDownloadHtmlResourcesAsync(responseCommand, request, httpClient, linkedCts.Token);
                        downloadWatch.Stop();
                        responseCommand.TotalTime = uploadWatch.Elapsed + downloadWatch.Elapsed + streamTime; // set the response time after the complete payload is read.
                        // Data Transmission Metrics 
                        await _metricsService.TryUpdateDataReceivedAsync(request.Id, dataReceivedSize, (downloadWatch.ElapsedMilliseconds + streamTime.TotalMilliseconds), linkedCts.Token);

                        lpsHttpResponse = new HttpResponse(responseCommand, _logger, _runtimeOperationIdProvider);
                        lpsHttpResponse.SetHttpRequest(request);

                        //Update Response Break Down Metrics
                        await _metricsService.TryUpdateResponseMetricsAsync(request.Id, lpsHttpResponse, linkedCts.Token);

                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {SessionId} - Request # {sequenceNumber} {httpRequestMessage?.Method} {httpRequestMessage?.RequestUri} Http/{httpRequestMessage?.Version}\n\tTotal Time: {responseCommand?.TotalTime.TotalMilliseconds} MS\n\tStatus Code: {(int)responseMessage?.StatusCode} Reason: {responseMessage?.ReasonPhrase}\n\tResponse Body: {responseCommand?.LocationToResponse}\n\tResponse Headers: {responseMessage?.Headers}{responseMessage?.Content?.Headers}", LPSLoggingLevel.Verbose, token);
                        //Update Throughput Metrics
                        await _metricsService.TryDecreaseConnectionsCountAsync(request.Id, responseMessage.IsSuccessStatusCode, linkedCts.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                //Decrease Connections On Failure
                await _metricsService.TryDecreaseConnectionsCountAsync(request.Id, false, token);

                HttpResponse.SetupCommand lpsResponseCommand = new()
                {
                    StatusCode = 0,
                    StatusMessage = ex?.InnerException?.Message ?? ex?.Message,
                    LocationToResponse = string.Empty,
                    IsSuccessStatusCode = false,
                    HttpRequestId = request.Id,
                    TotalTime = uploadWatch.Elapsed + downloadWatch.Elapsed + initialStreamTime,
                };

                lpsHttpResponse = new HttpResponse(lpsResponseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.SetHttpRequest(request);
                await _metricsService.TryUpdateResponseMetricsAsync(request.Id, lpsHttpResponse, token);


                if (ex.Message.Contains("socket") || ex.Message.Contains("buffer") || ex.InnerException != null && (ex.InnerException.Message.Contains("socket") || ex.InnerException.Message.Contains("buffer")))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"Client: {SessionId} - Request # {sequenceNumber} {request.HttpMethod} {request.Url.Url} Http/{request.HttpVersion} \n\t  The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Critical, token);
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, @$"...Client: {SessionId} - Request # {sequenceNumber} {request.HttpMethod} {request.Url.Url} Http/{request.HttpVersion} \n\t The request # {sequenceNumber} failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, token);
                throw;
            }
            return lpsHttpResponse;
        }
        private static async Task<HttpContent> WrapWithProgressContentAsync(HttpContent content, Stopwatch stopwatch, CancellationToken token)
        {
            var progress = new Progress<long>(bytesUploaded =>
            {
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
                    await htmlResourceDownloader.DownloadResourcesAsync(httpRequest.Url.Url, httpRequest.Id, token);
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



