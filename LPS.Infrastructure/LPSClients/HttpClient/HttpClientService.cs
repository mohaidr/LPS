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
using LPS.Infrastructure.LPSClients.MessageServices;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.ResponseService;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.LPSClients.CachService;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Extensions;
using System.Net.Security;
using System.Net.Sockets;
using LPS.Infrastructure.Monitoring.Metrics;
using Google.Protobuf.WellKnownTypes;
using System.IO;

namespace LPS.Infrastructure.LPSClients
{
    public class HttpClientService : IClientService<HttpRequest, HttpResponse>
    {
        readonly HttpClient httpClient;
        readonly ILogger _logger;
        private static int _clientNumber;
        public static object _lock = new();
        private readonly ConnectionInitializationService _connectionInitService = new();
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
                ConnectCallback = async (context, cancellationToken) =>
                {
                    // Measure DNS resolution time
                    var dnsStopwatch = Stopwatch.StartNew();
                    var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                    dnsStopwatch.Stop();

                    // Store DNS resolution time in request options
                    context.InitialRequestMessage.Options.Set(new HttpRequestOptionsKey<double>("DnsResolutionTime"), dnsStopwatch.Elapsed.TotalMilliseconds);

                    // Measure TCP connect time using resolved IP address
                    var tcpStopwatch = Stopwatch.StartNew();
                    var socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    await socket.ConnectAsync(addresses[0], context.DnsEndPoint.Port, cancellationToken);
                    tcpStopwatch.Stop();

                    // Store TCP handshake time in request options
                    context.InitialRequestMessage.Options.Set(new HttpRequestOptionsKey<double>("TcpHandshakeTime"), tcpStopwatch.Elapsed.TotalMilliseconds);

                    var netStream = new NetworkStream(socket, ownsSocket: true);

                    // Decide TLS only for HTTPS
                    bool isHttps =
                        string.Equals(context.InitialRequestMessage?.RequestUri?.Scheme,
                                      Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                        || context.DnsEndPoint.Port == 443;

                    if (!isHttps)
                    {
                        // HTTP: no TLS, so TLS time is 0
                        context.InitialRequestMessage.Options.Set(new HttpRequestOptionsKey<double>("TlsHandshakeTime"), 0.0);
                        return netStream;
                    }

                    // HTTPS: Measure TLS handshake time
                    var tlsStopwatch = Stopwatch.StartNew();
                    var ssl = new SslStream(netStream, leaveInnerStreamOpen: false,
                                            userCertificateValidationCallback: (_, __, ___, ____) => true);
                    await ssl.AuthenticateAsClientAsync(context.DnsEndPoint.Host);
                    tlsStopwatch.Stop();

                    // Store TLS handshake time in request options
                    context.InitialRequestMessage.Options.Set(new HttpRequestOptionsKey<double>("TlsHandshakeTime"), tlsStopwatch.Elapsed.TotalMilliseconds);

                    return ssl;
                }
            };
            httpClient = new HttpClient(socketsHandler)
            {
                Timeout = ((ILPSHttpClientConfiguration<HttpRequest>)config).Timeout
            };
            GuidId = Guid.NewGuid().ToString();
            lock (_lock)
            {
                SessionId = (++_clientNumber).ToString();
            }
        }

        public async Task<HttpResponse> SendAsync(HttpRequest httpRequestEntity, CancellationToken token = default)
        {
            HttpResponse lpsHttpResponse;
            Stopwatch timeToHeadersWatch = new();
            TimeSpan downStreamTime = TimeSpan.FromSeconds(0);
            TimeSpan htmlDownloadTime = TimeSpan.FromSeconds(0);
            double tcpHandshakeTime = 0;
            double tlsHandshakeTime = 0;
            double dnsResolutionTime = 0;
            double uploadTime = 0;
            HttpRequestMessage httpRequestMessage = null;
            try
            {
                using var timeoutCts = new CancellationTokenSource();
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                    {
                        timeoutCts.CancelAfter(httpClient.Timeout);

                        #region Build The Message
                        (httpRequestMessage, long dataSentSize) = await _messageService.BuildAsync(httpRequestEntity, this.SessionId, linkedCts.Token);
                        httpRequestMessage.Content = httpRequestMessage.Content != null ? WrapWithProgressContentAsync(httpRequestEntity, httpRequestMessage.Content, httpRequestMessage, linkedCts.Token) : httpRequestMessage.Content;
                        #endregion

                        //Update Throughput Metric
                        await _metricsService.TryIncreaseConnectionsCountAsync(httpRequestEntity.Id, token);

                        // Initialize connection for first request to host (OPTIONS pre-flight)
                        var optionsTiming = await _connectionInitService.InitializeConnectionAsync(
                            httpClient, 
                            httpRequestMessage, 
                            _logger,
                            linkedCts.Token);

                        //Start the http Request
                        timeToHeadersWatch.Start();
                        var responseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
                        timeToHeadersWatch.Stop();

                        // Extract connection timing: use OPTIONS timing if available, otherwise extract from actual request
                        var requestTiming = _connectionInitService.ExtractTimingFromRequest(httpRequestMessage);
                        
                        // Use OPTIONS timing (from InitializeConnection) if connection was reused, otherwise use actual request timing
                        dnsResolutionTime = optionsTiming.DnsResolutionMs > 0 ? optionsTiming.DnsResolutionMs : requestTiming.DnsResolutionMs;
                        tcpHandshakeTime = optionsTiming.TcpHandshakeMs > 0 ? optionsTiming.TcpHandshakeMs : requestTiming.TcpHandshakeMs;
                        tlsHandshakeTime = optionsTiming.TlsHandshakeMs > 0 ? optionsTiming.TlsHandshakeMs : requestTiming.TlsHandshakeMs;
                        
                        if (httpRequestMessage.Options.TryGetValue(new HttpRequestOptionsKey<double>("UploadTime"), out var upload))
                        {
                            uploadTime = upload;
                        }

                        #region Take Content Caching Decesion
                        string contentType = responseMessage?.Content?.Headers?.ContentType?.MediaType;
                        MimeType mimeType = MimeTypeExtensions.FromContentType(contentType);
                        bool captureResponse = httpRequestEntity.Capture != null && httpRequestEntity.Capture.IsValid;
                        bool cacheResponse = (mimeType == MimeType.TextHtml && httpRequestEntity.DownloadHtmlEmbeddedResources) || captureResponse;
                        #endregion

                        //Read the Response and total time spent to read the data represented as timespan
                        // TODO: Test moving the download and upload bytes metric to the ProgressContent and the  ResponseProcessingService Service to let the dashboard reflect them instantly for large payloads
                        (HttpResponse.SetupCommand command, double dataReceivedSize, downStreamTime) = (await _responseProcessingService.ProcessResponseAsync(responseMessage, httpRequestEntity, cacheResponse, linkedCts.Token));
                        HttpResponse.SetupCommand responseCommand = command;

                        #region Capture Response
                        if (captureResponse)
                        {
                            var rawContent = await _memoryCacheService.GetItemAsync($"{CachePrefixes.Content}{httpRequestEntity.Id}");


                            bool typeDetected = httpRequestEntity.Capture.As.TryToVariableType(out VariableType type);

                            // auto detect type if type if not provided, if not detected; default to string if not
                            if (!typeDetected)
                            {
                                switch (mimeType)
                                {
                                    case MimeType.ApplicationJson:
                                        type = VariableType.JsonString;
                                        break;
                                    case MimeType.RawXml:
                                    case MimeType.TextXml:
                                    case MimeType.ApplicationXml:
                                        type = VariableType.XmlString;
                                        break;

                                    case MimeType.TextCsv:
                                        type = VariableType.CsvString;
                                        break;
                                    default:
                                        type = VariableType.String;
                                        break;
                                }
                            }

                            var responseVariableHolder =
                                await _sessionManager.GetVariableAsync(this.SessionId, httpRequestEntity.Capture.To, token);

                            if (responseVariableHolder is null)
                            {
                                if (await _variableManager.GetAsync(httpRequestEntity.Capture.To, token) is IHttpResponseVariableHolder httpResponseVariable)
                                {
                                    responseVariableHolder = httpResponseVariable;
                                }
                            }

                            var bodyVariableHolder = ((IHttpResponseVariableHolder)responseVariableHolder)?.Body;

                            var responseVariableBuilder = responseVariableHolder?.Builder != null ? ((HttpResponseVariableHolder.VBuilder)responseVariableHolder.Builder) : new HttpResponseVariableHolder.VBuilder(_placeholderResolverService, _logger, _runtimeOperationIdProvider);
                            var stringVariableBuiler = bodyVariableHolder?.Builder != null ? ((StringVariableHolder.VBuilder)bodyVariableHolder?.Builder) : new StringVariableHolder.VBuilder(_placeholderResolverService, _logger, _runtimeOperationIdProvider);


                            bodyVariableHolder = rawContent != null ?
                                    (IStringVariableHolder)await stringVariableBuiler
                                    .WithType(type)
                                    .WithPattern(httpRequestEntity.Capture.Regex)
                                    .WithRawValue(rawContent)
                                    .BuildAsync(token)
                                : null;

                            responseVariableHolder = await responseVariableBuilder
                                 .WithBody(bodyVariableHolder)
                                 .WithHeaders(responseMessage.Headers.ToDictionary())
                                 .WithStatusCode(responseMessage.StatusCode)
                                 .WithStatusReason(responseMessage.ReasonPhrase)
                                 .BuildAsync(token);


                            if (httpRequestEntity.Capture.MakeGlobal == true)
                            {
                                responseVariableHolder = await responseVariableBuilder.SetGlobal().BuildAsync(token);
                                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Setting {(MimeTypeExtensions.IsTextContent(mimeType) ? rawContent : "BinaryContent ")} to {httpRequestEntity.Capture.To} as a global variable", LPSLoggingLevel.Verbose, linkedCts.Token);
                                await _variableManager.PutAsync(httpRequestEntity.Capture.To, responseVariableHolder, token);
                            }
                            else
                            {
                                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Setting {(MimeTypeExtensions.IsTextContent(mimeType) ? rawContent : "BinaryContent ")} to {httpRequestEntity.Capture.To} under Session {this.SessionId}", LPSLoggingLevel.Verbose, linkedCts.Token);
                                await _sessionManager.PutVariableAsync(this.SessionId, httpRequestEntity.Capture.To, responseVariableHolder, linkedCts.Token);
                            }
                        }

                        #endregion

                        // Download Html Embdedded Resources, conditonally
                        (bool hasDownloaded, htmlDownloadTime) = await TryDownloadHtmlResourcesAsync(responseCommand, httpRequestEntity, httpClient, linkedCts.Token);
                        // Total time includes connection establishment (DNS/TCP/TLS) + TTFB + Download + HTML resources
                        var connectionTime = TimeSpan.FromMilliseconds(dnsResolutionTime + tcpHandshakeTime + tlsHandshakeTime);
                        responseCommand.TotalTime = timeToHeadersWatch.Elapsed + htmlDownloadTime + downStreamTime + connectionTime;
                        lpsHttpResponse = new HttpResponse(responseCommand, _logger, _runtimeOperationIdProvider);
                        lpsHttpResponse.SetHttpRequest(httpRequestEntity);

                        //Update Response Break Down Metrics
                        await _metricsService.TryUpdateResponseMetricsAsync(httpRequestEntity.Id, responseCommand, linkedCts.Token);
                        await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.ReceivingTime, downStreamTime.TotalMilliseconds, linkedCts.Token); // RENAMED
                        await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.TotalTime, responseCommand.TotalTime.TotalMilliseconds, linkedCts.Token);

                        // Update TCP, TLS, Upload (Sending), TTFB, and Waiting Time metrics
                        await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.TCPHandshakeTime, tcpHandshakeTime, linkedCts.Token);
                        await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.TLSHandshakeTime, tlsHandshakeTime, linkedCts.Token);
                        await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.SendingTime, uploadTime, linkedCts.Token); // RENAMED
                        var ttfb = timeToHeadersWatch.Elapsed.TotalMilliseconds + dnsResolutionTime + tcpHandshakeTime + tlsHandshakeTime;
                        await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.TimeToFirstByte, ttfb, linkedCts.Token);
                        
                        // Calculate and update Waiting Time (TTFB - DNS - TCP - TLS - Upload)
                        double waitingTime = timeToHeadersWatch.Elapsed.TotalMilliseconds  - uploadTime;
                        await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.WaitingTime, waitingTime, linkedCts.Token);

                        await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {SessionId} - Request ID: {httpRequestEntity.Id} {httpRequestMessage?.Method} {httpRequestMessage?.RequestUri} Http/{httpRequestMessage?.Version}\n\tTotal Time: {responseCommand?.TotalTime.TotalMilliseconds} MS\n\tTTFB: {timeToHeadersWatch.Elapsed.TotalMilliseconds} MS\n\tWaiting: {waitingTime} MS\n\tDNS Resolution: {dnsResolutionTime} MS\n\tTCP Handshake: {tcpHandshakeTime} MS\n\tTLS Handshake: {tlsHandshakeTime} MS\n\tSending: {uploadTime} MS\n\tStatus Code: {(int)responseMessage?.StatusCode} Reason: {responseMessage?.ReasonPhrase}\n\tResponse Body: {responseCommand?.LocationToResponse}\n\tResponse Headers: {responseMessage?.Headers}{responseMessage?.Content?.Headers}", LPSLoggingLevel.Verbose, token);

                        //Update Throughput Metrics
                        await _metricsService.TryDecreaseConnectionsCountAsync(httpRequestEntity.Id, linkedCts.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                //Decrease Connections On Failure
                await _metricsService.TryDecreaseConnectionsCountAsync(httpRequestEntity.Id, token);

                HttpResponse.SetupCommand lpsResponseCommand = new()
                {
                    StatusCode = 0,
                    StatusMessage = ex?.InnerException?.Message ?? ex?.Message,
                    LocationToResponse = string.Empty,
                    IsSuccessStatusCode = false,
                    HttpRequestId = httpRequestEntity.Id,
                    TotalTime = timeToHeadersWatch.Elapsed + htmlDownloadTime + downStreamTime,
                };

                lpsHttpResponse = new HttpResponse(lpsResponseCommand, _logger, _runtimeOperationIdProvider);
                lpsHttpResponse.SetHttpRequest(httpRequestEntity);
                await _metricsService.TryUpdateResponseMetricsAsync(httpRequestEntity.Id, lpsResponseCommand, token);
                await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.ReceivingTime, downStreamTime.TotalMilliseconds, token); // RENAMED
                await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.TotalTime, lpsResponseCommand.TotalTime.TotalMilliseconds, token);

                // Update TCP, TLS, Sending, and Waiting metrics even on failure
                await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.TCPHandshakeTime, tcpHandshakeTime, token);
                await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.TLSHandshakeTime, tlsHandshakeTime, token);
                await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.SendingTime, uploadTime, token); // RENAMED
                
                // Calculate and update Waiting Time even on failure
                double waitingTime = timeToHeadersWatch.Elapsed.TotalMilliseconds - dnsResolutionTime - tcpHandshakeTime - tlsHandshakeTime - uploadTime;
                await _metricsService.TryUpdateDurationMetricAsync(httpRequestEntity.Id, DurationMetricType.WaitingTime, waitingTime, token);

                if (ex.Message.Contains("socket") || ex.Message.Contains("buffer") || ex.InnerException != null && (ex.InnerException.Message.Contains("socket") || ex.InnerException.Message.Contains("buffer")))
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {SessionId} - Request ID: {httpRequestEntity.Id} {httpRequestMessage?.Method} {httpRequestMessage?.RequestUri} Http/{httpRequestMessage?.Version}\n\t  The request (ID: {httpRequestEntity.Id}) failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Critical, token);
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {SessionId} - Request ID: {httpRequestEntity.Id} {httpRequestMessage?.Method} {httpRequestMessage?.RequestUri} Http/{httpRequestMessage?.Version}\n\t The request failed with the following exception  {(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, token);
                throw;
            }
            return lpsHttpResponse;
        }

        private HttpContent WrapWithProgressContentAsync(HttpRequest request, HttpContent content, HttpRequestMessage httpRequestMessage, CancellationToken token)
        {
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var progress = new Progress<long>(async (bytesRead) =>
            {
                await _metricsService.TryUpdateDataSentAsync(request.Id, bytesRead, token);
            });

            return new ProgressContent(content, progress, httpRequestMessage, token);
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
                    var htmlResourceDownloader = new HtmlResourceDownloaderService(_logger, _runtimeOperationIdProvider, client, _metricsService, _memoryCacheService);
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